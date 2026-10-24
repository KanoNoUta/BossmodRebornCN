namespace BossMod.Shadowbringers.Foray.CriticalEngagement.CE43OnSerpentsWings;

public enum OID : uint
{
    ElectricFence = 0x1EB170,
    StormborneZirnitra = 0x323B,
    WaveborneZirnitra = 0x323C,
    FlameborneZirnitra = 0x323D,
    ExplodingZirnitra = 0x323E,
    DeadWindZirnitra = 0x323F,
    DeadFireZirnitra = 0x3240,
    DeadWaterZirnitra = 0x3241,
    ImperialImaginifer = 0x3245
}

public enum AID : uint
{
    Turbine = 24148, // 9s visual; helper's damage/16y knockback follows ~0.85s later
    TurbineAOE = 24149,
    Degrees74 = 24150, // 8s cast, player-centered donut, outer radius 8
    FlamingCyclone = 24151, // 8s cast, radius 10; staggered or two groups 4s apart
    Explosion = 24152, // 20s cast, radius 10; kill the central snake first
    DeadWind = 24153,
    DeadWater = 24154,
    DeadFire = 24155
}

sealed class ElectricFence(BossModule module) : Components.GenericAOEs(module)
{
    private readonly AOEInstance[] _aoes = [new(new AOEShapeDonut(CE43OnSerpentsWings.SafeRadius, CE43OnSerpentsWings.ArenaRadius), module.Center)];
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => !Module.PrimaryActor.IsDestroyed ? _aoes : [];
    public override void AddHints(int slot, Actor actor, TextHints hints) { }
}

sealed class FlamingCyclone(BossModule module) : Components.SimpleAOEs(module, (uint)AID.FlamingCyclone, 10f)
{
    private bool _firstResolved;
    private bool _staggered;
    // Preserve the early circles as possible landing locations after their actual hits.
    public readonly List<AOEInstance> Cleared = [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != WatchedAction)
            return;
        if (Casters.Count == 0)
        {
            _firstResolved = _staggered = false;
            Cleared.Clear();
        }
        base.OnCastStarted(caster, spell);
        SortHelpers.SortAOEByActivation(Casters);
        // Opening: center, then eight circles ~0.54s apart. The knockback wave instead
        // starts four circles together, then five together; none of those may be hidden.
        if (Casters.Count >= 3)
            _staggered = (Casters[1].Activation - Casters[0].Activation).TotalSeconds is > 0.3 and < 1;
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var aoes = CollectionsMarshal.AsSpan(Casters);
        // Do not paint the final waiting circle until the central circle ACTUALLY hits.
        return _staggered && !_firstResolved && aoes.Length == 9 ? aoes[..^1] : aoes;
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell) { } // removal follows the damage event, not the visual cast bar

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID != WatchedAction)
            return;
        ++NumCasts;
        _firstResolved = true;
        for (var i = 0; i < Casters.Count; ++i)
        {
            if (Casters[i].ActorID != caster.InstanceID)
                continue;
            Cleared.Add(Casters[i]);
            Casters.RemoveAt(i);
            break;
        }
    }

    public override void Update() => Casters.RemoveAll(a => WorldState.Actors.Find(a.ActorID) is not { IsDeadOrDestroyed: false }
        || a.Activation.AddSeconds(1) < WorldState.CurrentTime);

    public override void AddHints(int slot, Actor actor, TextHints hints) { }
}

sealed class Degrees74(BossModule module) : Components.BaitAwayCast(module, (uint)AID.Degrees74, new AOEShapeDonut(2f, 8f), centerAtTarget: true, endsOnCastEvent: true)
{
    // Use a conservative 2y gathering core; all four targets must stay tightly together.
    private Actor? _anchor;
    public DateTime Activation => CurrentBaits.Count > 0 ? CurrentBaits[0].Activation : default;

    private static bool Alive(Actor actor) => !actor.IsDeadOrDestroyed;

    public WPos? GatheringPoint(Actor? viewer = null)
    {
        if (CurrentBaits.Count == 0)
        {
            _anchor = null;
            return null;
        }

        viewer ??= Raid.Player();
        var wind = Module.FindComponent<Turbine>();
        if (wind?.Resolving == true)
            return null;
        var targets = CurrentBaits.Where(b => Alive(b.Target)).Select(b => b.Target).Distinct().ToArray();
        var safeTargets = targets.Where(a => wind?.SafeForGathering(a, a.Position) != false
            && (viewer == null || wind?.SafeForGathering(viewer, a.Position) != false)).ToArray();
        // A minority staying in a dangerous pocket must never pull us out of the safe
        // knockback lane. With concurrent steel, gather only when most marks are safe.
        if (safeTargets.Length == 0 || wind?.HasLandingHazards == true && safeTargets.Length * 2 <= targets.Length)
        {
            _anchor = null;
            return null;
        }
        var forward = wind?.Caster is { } caster ? caster.Position + 5f * caster.Rotation.ToDirection() : Arena.Center;
        var allies = WorldState.Actors.Where(a => (a.Type == ActorType.Player || a.IsFriendlyNPC) && Alive(a)
            && a.Position.InCircle(Arena.Center, CE43OnSerpentsWings.SafeRadius)).ToArray();
        (int Marked, int Allies) Population(Actor target) =>
            (safeTargets.Count(a => a.Position.InCircle(target.Position, 2f)), allies.Count(a => a.Position.InCircle(target.Position, 3f)
                && wind?.SafeForGathering(a, a.Position) != false));

        Actor? best = null;
        var bestMarked = -1;
        var bestAllies = -1;
        var bestFront = float.MaxValue;
        foreach (var target in safeTargets)
        {
            // After the push there may be less than two seconds until water resolves.
            // Do not chase an otherwise safe group on the opposite side of the field.
            if (viewer != null && (target.Position - viewer.Position).Length() > Math.Max(1.25, (Activation - WorldState.CurrentTime).TotalSeconds * 6 + 1.25))
                continue;
            var (marked, count) = Population(target);
            var front = (target.Position - forward).LengthSq();
            // Gathering the marked players wins over facing/party membership.
            if (marked > bestMarked || marked == bestMarked && (count > bestAllies || count == bestAllies && front < bestFront))
            {
                best = target;
                bestMarked = marked;
                bestAllies = count;
                bestFront = front;
            }
        }
        if (best == null)
            return null;
        if (_anchor != null && safeTargets.Contains(_anchor)
            && (viewer == null || (_anchor.Position - viewer.Position).Length() <= Math.Max(1.25, (Activation - WorldState.CurrentTime).TotalSeconds * 6 + 1.25)))
        {
            var previous = Population(_anchor);
            if (previous.Marked == bestMarked && previous.Allies + 2 >= bestAllies)
                best = _anchor; // small movements must not flip the whole group's destination
        }
        _anchor = best;
        // Once the push has settled, aim inside the shared core instead of standing
        // on one mark's edge. Before the push preserve the selected landing ray.
        var cluster = safeTargets.Where(a => a.Position.InCircle(best.Position, 2f)).ToArray();
        var center = new WPos(cluster.Average(a => a.Position.X), cluster.Average(a => a.Position.Z));
        if (wind?.Casters.Count is not > 0 && cluster.Length > 1
            && cluster.All(a => center.InCircle(a.Position, 1.75f) && wind?.SafeForGathering(a, center) != false)
            && (viewer == null || wind?.SafeForGathering(viewer, center) != false))
            return center;
        return best.Position;
    }

    public override void DrawArenaBackground(int pcSlot, Actor pc)
    {
        // Include our own ring, too. Otherwise a marked player's circle is outline-only
        // and looks different from the other three. Never erase another ring's damage.
        foreach (var bait in CurrentBaits)
            if (Alive(bait.Target) && !bait.Source.IsDeadOrDestroyed)
                bait.Shape.Draw(Arena, bait.Target.Position, default);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var bait in CurrentBaits)
            if (Alive(bait.Target) && !bait.Source.IsDeadOrDestroyed)
                Arena.ZoneCircleOutline(bait.Target.Position, 2f, Colors.Border, 2f);
        if (GatheringPoint(pc) is WPos point)
            CE43OnSerpentsWings.DrawPosition(Arena, point);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // Marked players are still travelling during the push; their moving donuts
        // must not steer us away from the landing point before movement settles.
        if (Module.FindComponent<Turbine>()?.Resolving == true)
            return;
        var wind = Module.FindComponent<Turbine>();
        if (GatheringPoint(actor) is WPos point)
        {
            hints.GoalZones.Add(AIHints.GoalSingleTarget(point, 0.75f, 100f));
            // In the ordinary water wave the verified safe core avoids escaping
            // through the donut instead of staying with its marked players.
            if (wind?.HasLandingHazards != true && CurrentBaits.All(b => !Alive(b.Target) || b.Target.Position.InCircle(point, 8f)))
                hints.AddForbiddenZone(new SDInvertedCircle(point, 1.25f), Activation);
        }
        // Steel, the fence and the knockback projection remain hard avoidance zones.
        // Water is a soft grouping preference during their combination: do not make
        // all of the small landing pockets forbidden just to follow unsafe marks.
        if (wind?.HasLandingHazards == true)
            return;
        foreach (var bait in CurrentBaits)
            if (bait.Target != actor && Alive(bait.Target))
                hints.AddForbiddenZone(bait.Shape, bait.Target.Position, default, bait.Activation);
    }

    public override void OnActorDestroyed(Actor actor) => CurrentBaits.RemoveAll(b => b.Source == actor || b.Target == actor);
    public override void AddHints(int slot, Actor actor, TextHints hints) { }
}

sealed class Explosion(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Explosion, 10f)
{
    private Actor? _central;
    private bool _centralSeen;
    public bool CenterCleared => _centralSeen && _central is not { IsDeadOrDestroyed: false };
    public Actor? KillTarget => _central is { IsDeadOrDestroyed: false, IsTargetable: true } ? _central : null;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != WatchedAction)
            return;
        if (Casters.Count == 0)
        {
            _central = null;
            _centralSeen = false;
        }
        if (caster.Position.InCircle(Arena.Center, 2f))
        {
            _central = caster;
            _centralSeen = true;
        }
        base.OnCastStarted(caster, spell);
    }

    public override void Update()
    {
        Casters.RemoveAll(a => WorldState.Actors.Find(a.ActorID) is not { IsDeadOrDestroyed: false });
        if (Casters.Count > 0 && !_centralSeen)
        {
            _central = Module.Enemies((uint)OID.ExplodingZirnitra).FirstOrDefault(a => a.Position.InCircle(Arena.Center, 2f));
            // On reload the killed center can already have despawned. All nine casts
            // start together, so after the spawn window its absence means it was killed.
            _centralSeen = _central != null || Casters.All(a => a.Activation < WorldState.FutureTime(18));
        }
    }
    public override void OnActorDeath(Actor actor) => Casters.RemoveAll(a => a.ActorID == actor.InstanceID);
    public override void OnActorDestroyed(Actor actor) => Casters.RemoveAll(a => a.ActorID == actor.InstanceID);

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (KillTarget is { } target)
        {
            Arena.Actor(target, Colors.Object);
            Arena.ZoneCircleOutline(target.Position, 2f, Colors.Object, 2f);
        }
        else if (CenterCleared && Casters.Count > 0)
            CE43OnSerpentsWings.DrawPosition(Arena, Arena.Center);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (KillTarget is { } target)
        {
            foreach (var enemy in hints.PotentialTargets)
                enemy.Priority = enemy.Actor == target ? 2 : AIHints.Enemy.PriorityPointless;
        }
    }

    public override void AddHints(int slot, Actor actor, TextHints hints) { }
}

sealed class Turbine(BossModule module) : Components.SimpleKnockbacks(module, (uint)AID.Turbine, 16f, maxCasts: 1)
{
    public Actor? Caster { get; private set; }
    private readonly FlamingCyclone _fire = module.FindComponent<FlamingCyclone>()!;
    private readonly Explosion _explosion = module.FindComponent<Explosion>()!;
    private DateTime _castFinish;
    private DateTime _resolveUntil;
    private WPos? _preferredPosition;
    public bool Resolving => WorldState.CurrentTime < _resolveUntil;
    public bool HasLandingHazards => _fire.Casters.Count > 0 || _explosion.Casters.Count > 0;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != WatchedAction)
            return;
        Caster = caster;
        _preferredPosition = null;
        _resolveUntil = default;
        _castFinish = Module.CastFinishAt(spell);
        Casters.Clear();
        Casters.Add(new(caster.Position, Distance, _castFinish.AddSeconds(0.85), actorID: caster.InstanceID));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell) { }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        // The helper splits the raid into two ActionEffects. Do not end another
        // player's prediction before the packet that actually affects them.
        if (spell.Action.ID == (uint)AID.TurbineAOE && Caster != null && caster.Position.InCircle(Caster.Position, 1f)
            && spell.Targets.Any(t => t.ID == Raid.Player()?.InstanceID))
        {
            Casters.Clear();
            _resolveUntil = WorldState.FutureTime(0.8); // 16y at the replay's 20y/s knockback speed
        }
    }

    public override void Update()
    {
        if (Casters.Count > 0 && (Caster?.IsDestroyed == true && WorldState.CurrentTime < _castFinish
            || Casters[0].Activation.AddSeconds(0.5) < WorldState.CurrentTime))
            Casters.Clear();
    }

    private WPos[] LandingHazards(DateTime knockback) => _fire.Casters.Where(a => a.Activation > knockback.AddSeconds(0.4)).Select(a => a.Origin)
        .Concat(_explosion.Casters.Select(a => a.Origin)).ToArray();

    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos)
    {
        if (!pos.InCircle(Arena.Center, CE43OnSerpentsWings.SafeRadius - 0.5f))
            return true;
        return Casters.Count > 0 && LandingHazards(Casters[0].Activation).Any(p => pos.InCircle(p, 10.5f));
    }

    private bool Immune(Actor actor, DateTime at)
    {
        var slot = Raid.FindSlot(actor.InstanceID);
        return slot >= 0 && IsImmune(slot, at) || actor.Statuses.Any(s => s.ExpireAt > at && s.ID is 160 or 1209 or 2345 or 1722 or (uint)WAR.SID.InnerStrength);
    }

    public bool SafeForGathering(Actor actor, WPos position)
    {
        if (!position.InCircle(Arena.Center, CE43OnSerpentsWings.SafeRadius - 0.5f))
            return false;
        if (Casters.Count > 0)
        {
            var kb = Casters[0];
            if (!Immune(actor, kb.Activation))
            {
                if (position.InCircle(kb.Origin, 0.5f))
                    return false;
                position = AwayFromSource(position, kb.Origin, Distance);
            }
            return !DestinationUnsafe(0, actor, position);
        }
        return !_fire.Casters.Concat(_explosion.Casters).Any(a => position.InCircle(a.Origin, 10.5f));
    }

    private ActionID ImmunityAction(Actor actor)
    {
        var id = actor.ClassCategory is ClassCategory.Healer or ClassCategory.Caster or ClassCategory.Limited ? ActionDefinitions.Surecast : ActionDefinitions.Armslength;
        var def = ActionDefinitions.Instance?[id];
        return def != null && def.AllowedClasses[(int)actor.Class] && actor.Level >= def.MinLevel
            && def.ReadyIn(WorldState.Client.Cooldowns, WorldState.Client.DutyActions) <= 0.1f ? id : default;
    }

    private bool PrepareImmunity(int slot, Actor actor) => _explosion.Casters.Count > 0 && _explosion.CenterCleared
        && Casters.Count > 0 && (IsImmune(slot, Casters[0].Activation) || ImmunityAction(actor) != default);

    public WPos? PositionForKnockback(int slot, Actor actor)
    {
        if (Casters.Count == 0 || IsImmune(slot, Casters[0].Activation) || PrepareImmunity(slot, actor))
            return null;
        var kb = Casters[0];
        if (Module.FindComponent<Degrees74>()?.GatheringPoint(actor) is WPos gathering && SafeForGathering(actor, gathering))
            return gathering;
        if (_preferredPosition is WPos cached && SafeForGathering(actor, cached)
            && !_fire.Casters.Any(a => a.Activation < kb.Activation && a.Check(cached))
            && (cached - actor.Position).LengthSq() < 16f)
            return cached;
        WPos? best = null;
        var distance = float.MaxValue;
        void Consider(WPos point)
        {
            if (!SafeForGathering(actor, point) || _fire.Casters.Any(a => a.Activation < kb.Activation && a.Check(point)))
                return;
            var d = (point - actor.Position).LengthSq();
            if (d < distance)
            {
                distance = d;
                best = point;
            }
        }
        foreach (var aoe in _fire.Casters.Concat(_fire.Cleared))
        {
            if (aoe.Activation >= kb.Activation.AddSeconds(-0.5))
                continue;
            var delta = aoe.Origin - kb.Origin;
            var length = delta.Length();
            if (length <= Distance + 1f || DestinationUnsafe(slot, actor, aoe.Origin))
                continue;
            var point = kb.Origin + (length - Distance) / length * delta;
            if (!point.InCircle(Arena.Center, CE43OnSerpentsWings.SafeRadius - 1f))
                continue;
            Consider(point);
        }
        // The slow-kill combination has no early circles: a central 16y push must
        // land between four diagonal circles. Reuse the same landing safety predicate
        // while sampling candidate standing points; no facing restriction is involved.
        if (best == null)
            for (var angle = 0; angle < 360; angle += 5)
                for (var radius = 1.5f; radius <= 12f; radius += 1.5f)
                    Consider(kb.Origin + radius * angle.Degrees().ToDirection());
        if (_preferredPosition is WPos previous && SafeForGathering(actor, previous)
            && !_fire.Casters.Any(a => a.Activation < kb.Activation && a.Check(previous))
            && (previous - actor.Position).LengthSq() <= distance + 4f)
            best = previous;
        _preferredPosition = best;
        return best;
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        base.DrawArenaForeground(pcSlot, pc);
        if (PositionForKnockback(pcSlot, pc) is WPos point)
            CE43OnSerpentsWings.DrawPosition(Arena, point);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (Casters.Count == 0)
            return;
        var kb = Casters[0];
        if (PrepareImmunity(slot, actor))
        {
            // Do not spend immunity on the earlier water/fire combinations, or before
            // killing the central Explosion. Six-second buffs must cover the helper hit.
            if (!IsImmune(slot, kb.Activation) && (kb.Activation - WorldState.CurrentTime).TotalSeconds <= 5)
                hints.ActionsToExecute.Push(ImmunityAction(actor), actor, ActionQueue.Priority.High);
            hints.GoalZones.Add(AIHints.GoalSingleTarget(Arena.Center, 2f, 100f));
            hints.AddForbiddenZone(new SDInvertedCircle(Arena.Center, 4f), kb.Activation);
            return;
        }
        if (IsImmune(slot, kb.Activation))
            return;
        var hazards = LandingHazards(kb.Activation);
        hints.AddForbiddenZone(new SDKnockbackInCircleAwayFromOriginPlusAOECircles(Arena.Center, kb.Origin, Distance,
            CE43OnSerpentsWings.SafeRadius - 1f, hazards, 10.5f, hazards.Length), kb.Activation);
        if (PositionForKnockback(slot, actor) is WPos point)
            hints.GoalZones.Add(AIHints.GoalSingleTarget(point, 0.5f, 50f));
    }

    public override void AddHints(int slot, Actor actor, TextHints hints) { }
}

sealed class CE43OnSerpentsWingsStates : StateMachineBuilder
{
    public CE43OnSerpentsWingsStates(CE43OnSerpentsWings module) : base(module)
    {
        // The fence survives all reinforcement gaps, including the final three snakes.
        SimplePhase(0, id => SimpleState(id, 10000f, ""), "")
            .ActivateOnEnter<ElectricFence>()
            .ActivateOnEnter<FlamingCyclone>()
            .ActivateOnEnter<Degrees74>()
            .ActivateOnEnter<Explosion>()
            .ActivateOnEnter<Turbine>()
            .Raw.Update = () => module.PrimaryActor.IsDestroyed;
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed, Contributors = "COXOIDA", PrimaryActorOID = (uint)OID.ElectricFence,
    GroupType = BossModuleInfo.GroupType.CriticalEngagement, GroupID = 778, NameID = 17)]
public sealed class CE43OnSerpentsWings(WorldState ws, Actor primary) : BossModule(ws, primary, arena.Center, arena)
{
    public const float ArenaRadius = 25f, SafeRadius = 20f;
    public static readonly WPos ArenaCenter = new(500f, 800f);
    // Same explicit polygon-based arena construction as CE23FiresOfWar.
    private static readonly ArenaBoundsCustom arena = new([new Polygon(ArenaCenter, ArenaRadius, 64)]);
    public static readonly uint[] Trash = [(uint)OID.StormborneZirnitra, (uint)OID.WaveborneZirnitra, (uint)OID.FlameborneZirnitra,
        (uint)OID.ExplodingZirnitra, (uint)OID.DeadWindZirnitra, (uint)OID.DeadWaterZirnitra, (uint)OID.DeadFireZirnitra, (uint)OID.ImperialImaginifer];

    internal static void DrawPosition(MiniArena arena, WPos point) => arena.AddQuad(point + new WDir(0, -0.8f), point + new WDir(0.8f, 0),
        point + new WDir(0, 0.8f), point + new WDir(-0.8f, 0), Colors.Safe, 2f);

    protected override bool CheckPull() => !PrimaryActor.IsDestroyed && Raid.Player() is { } pc && pc.Position.InCircle(ArenaCenter, ArenaRadius);
    public override bool ShouldPrioritizeAllEnemies => true;

    public override Actor? GetDefaultTarget(int slot) => FindComponent<Explosion>()?.KillTarget
        ?? Enemies(Trash).FirstOrDefault(a => a.IsTargetable && !a.IsDeadOrDestroyed && a.Position.InCircle(ArenaCenter, SafeRadius));

    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actors(this, Trash);
}
