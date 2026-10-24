namespace BossMod.Shadowbringers.Foray.CriticalEngagement.CE23FiresOfWar;

public enum OID : uint
{
    Boss = 0x2E75, // R2.55, pyrobolus pater
    ImperialPyromancer1 = 0x2E72, // R0.5
    ImperialPyromancer2 = 0x2E73, // R0.5
    TowerVisual1 = 0x1EB040, // R0.5
    TowerVisual2 = 0x1EB041, // R0.5
    PyrobolusSoror = 0x2E76, // R0.9
    WanderingFlame = 0x2E78, // R1.5
    PyrobolusMater = 0x2E74, // R3.75
    PyrobolusFrater = 0x2E77, // R0.6
    Helper = 0x233C
}

public enum AID : uint
{
    AutoAttack = 872, // Boss->player, no cast, single-target
    Fire = 21256, // ImperialPyromancer1->player, 2.5s cast, single-target
    MagickedMark = 21258, // ImperialPyromancer2->player, 2.5s cast, single-target
    ScaldingScolding = 21255, // Boss->self, no cast, range 8 120-degree cone
    Pyrokinesis = 21257, // ImperialPyromancer1->player, 4.5s cast, single-target

    Pyroplexy = 20659, // ImperialPyromancer1->location, 12.0s cast, range 4 circle, tower
    Pyroclysm = 20660, // Helper->location, no cast, range 40 circle, tower fail

    CantTouchThis = 20661, // PyrobolusSoror->self, no cast, range 6 circle, stack
    OutWithABang = 20662, // PyrobolusSoror->self, no cast, range 40 circle, stack fail

    ExplosiveCountdown1 = 20665, // Helper->self, 2.0s cast, range 11-18 donut
    ExplosiveCountdown2 = 20664, // Helper->self, 2.0s cast, range 5-12 donut
    ExplosiveCountdown3 = 20663, // Helper->self, 2.0s cast, range 6 circle

    CatchingFire = 20666, // WanderingFlame->self, 5.0s cast, range 40 circle, small raidwide
    TooHotToHandle = 20667, // PyrobolusFrater->self, 15.0s cast, range 10 circle
    HotTemper = 20668, // PyrobolusMater->self, 15.0s cast, range 30 circle, proximity AOE

    SlowDeflagration = 20669, // PyrobolusMater->self, 75.0s cast, range 40 circle, enrage
    RapidDeflagration = 20670 // PyrobolusMater->self, no cast, range 40 circle, enrage repeat
}

public enum SID : uint
{
    CantTouchThis = 2056 // none->PyrobolusSoror, extra=0xB9
}

sealed class ScaldingScolding(BossModule module) : Components.Cleave(module, (uint)AID.ScaldingScolding, new AOEShapeCone(8f, 60f.Degrees()));

sealed class CantTouchThis(BossModule module) : Components.GenericStackSpread(module)
{
    private readonly Dictionary<ulong, ulong> _preferredStacks = [];
    private static bool CanParticipate(Actor actor) => !actor.IsDeadOrDestroyed && actor.HPMP.MaxHP > 0 && actor.HPRatio >= 0.3f;

    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        // This status is also used by other actors with a different extra. Only soror's B9 marker is a contact stack.
        if (actor.OID == (uint)OID.PyrobolusSoror && status.ID == (uint)SID.CantTouchThis && status.Extra == 0xB9)
        {
            Stacks.RemoveAll(s => s.Target == actor);
            Stacks.Add(new(actor, 6f, 2)); // resolves when touched, rather than after a fixed countdown
        }
    }

    public override void OnStatusLose(Actor actor, ref ActorStatus status)
    {
        if (status.ID == (uint)SID.CantTouchThis)
            Stacks.RemoveAll(s => s.Target == actor);
    }

    public override void OnActorDeath(Actor actor) => Stacks.RemoveAll(s => s.Target == actor);
    public override void OnActorDestroyed(Actor actor) => Stacks.RemoveAll(s => s.Target == actor);

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        // Soror remains untargetable throughout the mechanic; draw the marker independently of enemy rendering.
        for (var i = 0; i < Stacks.Count; ++i)
            Arena.ZoneCircleOutline(Stacks[i].Target.Position, Stacks[i].Radius, Colors.Safe);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.CantTouchThis or (uint)AID.OutWithABang)
        {
            var count = Stacks.Count;
            for (var i = 0; i < count; ++i)
            {
                if (Stacks[i].Target == caster)
                {
                    Stacks.RemoveAt(i);
                    return;
                }
            }
        }
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (!CanParticipate(actor))
        {
            if (Stacks.Count > 0)
                hints.Add("血量低于30%，避开焰球分摊！", Stacks.Any(s => actor.Position.InCircle(s.Target.Position, s.Radius + 0.5f)));
            return;
        }
        if (Module.FindComponent<Pyroplexy>()?.PreferredTower(actor) != null)
            return;
        if (PreferredStack(actor) is Actor bomb)
            hints.Add(OtherSoakers(bomb, actor) > 0 ? "与其他玩家一起分摊焰球！" : "等待其他玩家集合，不要单吃焰球！", !actor.Position.InCircle(bomb.Position, 6f));
    }

    private int OtherSoakers(Actor bomb, Actor actor)
    {
        var count = 0;
        // Count the entire battlefield, including players outside our party and friendly combat NPCs.
        foreach (var ally in WorldState.Actors)
            if ((ally.Type == ActorType.Player || ally.IsFriendlyNPC) && ally != actor && !ally.IsDeadOrDestroyed
                && Arena.InBounds(ally.Position) && ally.Position.InCircle(bomb.Position, 6f))
                ++count;
        return count;
    }

    public Actor? PreferredStack(Actor actor)
    {
        if (!CanParticipate(actor))
        {
            _preferredStacks.Remove(actor.InstanceID);
            return null;
        }
        Actor? best = null;
        Actor? previous = null;
        var bestCount = -1;
        var bestDistance = float.MaxValue;
        _preferredStacks.TryGetValue(actor.InstanceID, out var previousID);
        for (var i = 0; i < Stacks.Count; ++i)
        {
            var bomb = Stacks[i].Target;
            if (bomb.IsDeadOrDestroyed)
                continue;
            if (bomb.InstanceID == previousID)
                previous = bomb;
            var count = OtherSoakers(bomb, actor);
            var distance = (bomb.Position - Arena.Center).Length();
            if (count > bestCount || count == bestCount && distance < bestDistance)
            {
                best = bomb;
                bestCount = count;
                bestDistance = distance;
            }
        }
        // Ignore small movements of equally populated bombs instead of switching destination every frame.
        if (previous != null && OtherSoakers(previous, actor) == bestCount && (previous.Position - Arena.Center).Length() <= bestDistance + 1f)
            best = previous;
        if (best != null)
            _preferredStacks[actor.InstanceID] = best.InstanceID;
        else
            _preferredStacks.Remove(actor.InstanceID);
        return best;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (!CanParticipate(actor))
        {
            for (var i = 0; i < Stacks.Count; ++i)
                hints.AddForbiddenZone(new SDCircle(Stacks[i].Target.Position, Stacks[i].Radius + 0.5f));
            return;
        }
        // Towers can overlap the contact stacks. Never pull a sole tower soaker away to a bomb.
        if (Module.FindComponent<Pyroplexy>()?.PreferredTower(actor) != null)
            return;
        if (PreferredStack(actor) is not Actor bomb)
            return;

        var inward = Arena.Center - bomb.Position;
        var direction = inward.LengthSq() > 0.01f ? inward.Normalized() : new WDir(0, 1);
        var hasOthers = OtherSoakers(bomb, actor) > 0;
        var gatheringPoint = hasOthers ? bomb.Position : bomb.Position + 3f * direction;
        // Touch the bomb only with allies already in range; otherwise wait on its inward side for a group.
        hints.GoalZones.Add(AIHints.GoalSingleTarget(gatheringPoint, hasOthers ? 0.25f : 1f, 50f));
        hints.AddForbiddenZone(new SDInvertedCircle(bomb.Position, 5f));
        for (var i = 0; i < Stacks.Count; ++i)
        {
            var target = Stacks[i].Target;
            if (OtherSoakers(target, actor) == 0)
                hints.AddForbiddenZone(new SDCircle(target.Position, 2f)); // gather inside the shared AOE without triggering a solo contact
        }
    }
}

sealed class Pyroplexy(BossModule module) : Components.CastTowersOpenWorld(module, (uint)AID.Pyroplexy, 4f, maxSoakers: int.MaxValue)
{
    private readonly Dictionary<ulong, Tower> _targets = [];

    private int NumSoakers(Tower tower)
    {
        var count = 0;
        foreach (var player in WorldState.Actors)
            if (player.Type == ActorType.Player && !player.IsDeadOrDestroyed && tower.IsInside(player))
                ++count;
        return count;
    }

    public Tower? PreferredTower(Actor actor)
    {
        Tower? current = null;
        Tower? best = null;
        var bestDistance = float.MaxValue;
        var speed = WorldState.Client.MoveSpeed;
        if (!float.IsFinite(speed) || speed <= 0)
            speed = 6f;
        bool CanReach(Tower tower)
        {
            var distance = Math.Max(0f, (tower.Position - actor.Position).Length() - (Radius - 1f));
            return distance / speed + 0.75f < (tower.Activation - WorldState.CurrentTime).TotalSeconds;
        }
        Tower? Remember(Tower? tower)
        {
            if (tower != null)
                _targets[actor.InstanceID] = tower;
            else
                _targets.Remove(actor.InstanceID);
            return tower;
        }
        for (var i = 0; i < Towers.Count; ++i)
        {
            var tower = Towers[i];
            var count = NumSoakers(tower);
            if (tower.IsInside(actor))
            {
                if (count == 1)
                    return Remember(tower); // leaving would make this tower empty
                current ??= tower;
            }
            if (count != 0)
                continue;
            // Allow time to get comfortably inside the circle, including a small movement/latency margin.
            var distance = Math.Max(0f, (tower.Position - actor.Position).Length() - (Radius - 1f));
            if (CanReach(tower) && distance < bestDistance)
            {
                best = tower;
                bestDistance = distance;
            }
        }
        // A passerby entering our destination must not cancel it or make us turn around mid-route.
        // Once inside, an extra soaker may still move on to another reachable empty tower.
        if (current == null && _targets.TryGetValue(actor.InstanceID, out var target) && Towers.Contains(target) && CanReach(target))
            return target;
        return Remember(best ?? current);
    }

    public override void DrawArenaBackground(int pcSlot, Actor pc) { } // occupied towers are never danger zones

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        for (var i = 0; i < Towers.Count; ++i)
            Towers[i].Shape.Outline(Arena, Towers[i].Position, Towers[i].Rotation, Colors.Safe, 2f);
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (PreferredTower(actor) is Tower tower)
            hints.Add(tower.IsInside(actor) ? "留在塔内！" : "补可及时到达的空塔！", !tower.IsInside(actor));
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (PreferredTower(actor) is Tower tower)
        {
            var inside = tower.IsInside(actor);
            // Towers only provide a soft movement goal. An inverted forbidden zone here would also
            // forbid every other tower, including occupied towers, and cause occupancy-driven oscillation.
            hints.GoalZones.Add(AIHints.GoalSingleTarget(inside ? actor.Position : tower.Position, inside ? 0.5f : Radius - 1f, 100f));
        }
        BitMask mask = default;
        foreach (var (partySlot, player) in Raid.WithSlot())
            for (var i = 0; i < Towers.Count; ++i)
                if (Towers[i].IsInside(player))
                    mask.Set(partySlot);
        if (Towers.Count != 0)
            hints.AddPredictedDamage(mask, Towers[0].Activation, DamageType);
    }
}

sealed class CatchingFire(BossModule module) : Components.RaidwideCast(module, (uint)AID.CatchingFire);

sealed class TooHotToHandle(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCircle circle = new(10f);
    private readonly List<AOEInstance> _aoes = [with(9)];

    // All nine bombs are dangerous until one actually dies. Do not turn the intended kill target into a false safe spot.
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_aoes);

    public Actor? KillTarget()
    {
        if (_aoes.Count != 9 || ((CE23FiresOfWar)Module).BossMater() is not Actor mater)
            return null;

        Actor? furthest = null;
        var maxDistSq = float.MinValue;
        for (var i = 0; i < _aoes.Count; ++i)
        {
            var bomb = WorldState.Actors.Find(_aoes[i].ActorID);
            if (bomb is { IsDeadOrDestroyed: false })
            {
                var distSq = (bomb.Position - mater.Position).LengthSq();
                if (distSq > maxDistSq)
                {
                    maxDistSq = distSq;
                    furthest = bomb;
                }
            }
        }
        return furthest;
    }

    public override void AddGlobalHints(GlobalHints hints)
    {
        if (KillTarget() != null)
            hints.Add("击杀离焰球之母最远的焰球之兄，腾出安全区！");
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (KillTarget() is Actor bomb)
        {
            Arena.Actor(bomb, Colors.Object);
            Arena.ZoneCircleOutline(bomb.Position, 1.5f, Colors.Object, 2f);
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.TooHotToHandle)
        {
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
            _aoes.Add(new(circle, spell.LocXZ, default, Module.CastFinishAt(spell), actorID: caster.InstanceID));
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.TooHotToHandle)
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
    }

    public override void OnActorDeath(Actor actor) => _aoes.RemoveAll(a => a.ActorID == actor.InstanceID);
    public override void OnActorDestroyed(Actor actor) => _aoes.RemoveAll(a => a.ActorID == actor.InstanceID);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var count = _aoes.Count;
        if (KillTarget() is Actor target)
        {
            var aoes = CollectionsMarshal.AsSpan(_aoes);
            for (var i = 0; i < count; ++i)
            {
                ref readonly var aoe = ref aoes[i];
                if (aoe.ActorID != target.InstanceID)
                    hints.AddForbiddenZone(aoe.Shape, aoe.Origin, default, aoe.Activation);
            }
            var countT = hints.PotentialTargets.Count;
            for (var i = 0; i < countT; ++i)
            {
                var e = hints.PotentialTargets[i];
                e.Priority = e.Actor == target ? 1 : AIHints.Enemy.PriorityPointless;
            }
        }
        else if (count > 0)
        {
            base.AddAIHints(slot, actor, assignment, hints);
            var countT = hints.PotentialTargets.Count;
            for (var i = 0; i < countT; ++i)
            {
                hints.PotentialTargets[i].Priority = AIHints.Enemy.PriorityPointless;
            }
        }
    }
}

sealed class ExplosiveCountdown(BossModule module) : Components.ConcentricAOEs(module, _shapes)
{
    private static readonly AOEShape[] _shapes = [new AOEShapeDonut(11f, 18f), new AOEShapeDonut(5f, 12f), new AOEShapeCircle(6f)];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.ExplosiveCountdown1)
            AddSequence(spell.LocXZ, Module.CastFinishAt(spell));
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (Sequences.Count != 0)
        {
            var order = spell.Action.ID switch
            {
                (uint)AID.ExplosiveCountdown1 => 0,
                (uint)AID.ExplosiveCountdown2 => 1,
                (uint)AID.ExplosiveCountdown3 => 2,
                _ => -1
            };
            AdvanceSequence(order, caster.Position, WorldState.FutureTime(4d));
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.OID == (uint)OID.WanderingFlame)
        {
            Sequences.Clear();
        }
    }
}

sealed class HotTemper(BossModule module) : Components.SimpleAOEs(module, (uint)AID.HotTemper, 24f);
sealed class SlowDeflagration(BossModule module) : Components.CastHint(module, (uint)AID.SlowDeflagration, "Enrage!", true);

sealed class CE23FiresOfWarStates : StateMachineBuilder
{
    public CE23FiresOfWarStates(CE23FiresOfWar module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<CantTouchThis>()
            .ActivateOnEnter<Pyroplexy>()
            .ActivateOnEnter<CatchingFire>()
            .ActivateOnEnter<ExplosiveCountdown>()
            .ActivateOnEnter<HotTemper>()
            .ActivateOnEnter<TooHotToHandle>()
            .ActivateOnEnter<SlowDeflagration>()
            .ActivateOnEnter<ScaldingScolding>()
            // Reinforcements spawn after gaps with no living trash. Only the final mother's death ends this CE.
            .Raw.Update = () => module.BossMater()?.IsDeadOrDestroyed ?? false;
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Verified, Contributors = "The Combat Reborn Team (Malediktus)", GroupType = BossModuleInfo.GroupType.CriticalEngagement, GroupID = 735, NameID = 9)]
public sealed class CE23FiresOfWar(WorldState ws, Actor primary) : BossModule(ws, primary, arena.Center, arena)
{
    private static readonly ArenaBoundsCustom arena = new([new Polygon(new(83f, 563f), 19.5f, 32)]);
    public static readonly uint[] Trash = [(uint)OID.Boss, (uint)OID.ImperialPyromancer1, (uint)OID.ImperialPyromancer2, (uint)OID.PyrobolusFrater];
    private Actor? _bossMater;
    public Actor? BossMater() => _bossMater;

    protected override bool CheckPull() => base.CheckPull() && Raid.Player()!.Position.InCircle(Arena.Center, 20f);

    protected override void UpdateModule()
    {
        _bossMater ??= GetActor((uint)OID.PyrobolusMater);
    }

    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(_bossMater);
        Arena.Actors(this, Trash);
    }
}
