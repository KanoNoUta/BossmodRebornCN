namespace BossMod.Shadowbringers.Foray.CriticalEngagement.CE61LeanMeanMagitekMachines;

public enum OID : uint
{
    MagitekInfantry = 0x31B9,
    GraceBeforeTheFall = 0x31BA,
    MagitekSkyArmor = 0x31BB,
    MagitekHunter = 0x31BC,
    MagitekSupport = 0x31BD,
    DeathMachine = 0x31BE,
    Boss = 0x31BF, // Kamp; appears after the reinforcement waves
    ElectricFence = 0x1EB178,
    Helper = 0x233C
}

public enum AID : uint
{
    GraceBeforeTheFallVisual = 23796, // Infantry summons its untargetable copy ~0.1s later
    GraceBeforeTheFall = 23797, // Copy, 5s cast, 50x8 rectangle
    GraceBeforeTheFallLong = 23798,
    CubeRay = 23799, // Support, 6s cast, 20x20 square centered on the actor
    Napalm = 23800, // Support, 4s cast, radius 8
    MagitekWildfireFirst = 22828, // Death machine, 8s cast, 60x8 rectangle
    MagitekWildfireFirstAlt = 23802,
    MagitekWildfireRest = 23803, // Nine more beams, ~1.43s / 10 degrees apart
    HighPoweredMagitekRay = 23804,
    ElectromagneticField = 23806,
    ForeAndAftVisual = 23807,
    ForeFirst = 23808, // Helper, 5s cast, 21x10 rectangle
    ForeRest = 23809,
    AftFirst = 23810, // Helper, 5s cast, 22x10 rectangle
    AftRest = 23811
}

public enum IconID : uint
{
    RotateCW = 156,
    RotateCCW = 157
}

sealed class ElectricFence(BossModule module) : Components.GenericAOEs(module)
{
    // Keep the full square visible, as in CE64, and draw its electrified border separately.
    private static readonly AOEShapeCustom fence = new(
        [new Rectangle(CE61LeanMeanMagitekMachines.ArenaCenter, CE61LeanMeanMagitekMachines.ArenaHalfSize, CE61LeanMeanMagitekMachines.ArenaHalfSize)],
        [new Rectangle(CE61LeanMeanMagitekMachines.ArenaCenter, CE61LeanMeanMagitekMachines.SafeHalfSize, CE61LeanMeanMagitekMachines.SafeHalfSize)]);
    private readonly List<Actor> _fences = module.Enemies((uint)OID.ElectricFence);
    private readonly AOEInstance[] _aoes = [new(fence, module.Center)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _fences.Any(f => !f.IsDestroyed) ? _aoes : [];
    public override void AddHints(int slot, Actor actor, TextHints hints) { }
}

sealed class GraceBeforeTheFall(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeRect rect = new(50f, 4f);
    private readonly List<AOEInstance> _aoes = [];
    private readonly HashSet<ulong> _resolved = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_aoes);
    public override void AddHints(int slot, Actor actor, TextHints hints) { }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster.OID == (uint)OID.MagitekInfantry && spell.Action.ID == (uint)AID.GraceBeforeTheFallVisual)
        {
            // The summon event precedes the 5s cast by ~4.1s. Do not wait for that cast.
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
            _aoes.Add(new(rect, caster.Position.Quantized(), spell.Rotation, WorldState.FutureTime(9.1d), actorID: caster.InstanceID));
        }
        else if (spell.Action.ID is (uint)AID.GraceBeforeTheFall or (uint)AID.GraceBeforeTheFallLong && caster.OID == (uint)OID.GraceBeforeTheFall)
        {
            _resolved.Add(caster.InstanceID);
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID || a.Origin.AlmostEqual(caster.Position, 0.5f));
            ++NumCasts;
        }
    }

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID != (uint)OID.GraceBeforeTheFall || _resolved.Contains(actor.InstanceID))
            return;
        // Adopt the summoned actor, retaining the earlier event's deadline. Also supports loading during the summon.
        var index = _aoes.FindIndex(a => a.ActorID == actor.InstanceID || a.Origin.AlmostEqual(actor.Position, 0.5f));
        var activation = index >= 0 ? _aoes[index].Activation : WorldState.FutureTime(9d);
        if (index >= 0)
            _aoes.RemoveAt(index);
        _aoes.Add(new(rect, actor.Position.Quantized(), actor.Rotation, activation, actorID: actor.InstanceID));
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.GraceBeforeTheFall or (uint)AID.GraceBeforeTheFallLong && !_resolved.Contains(caster.InstanceID))
        {
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID || a.Origin.AlmostEqual(caster.Position, 0.5f));
            _aoes.Add(new(rect, spell.LocXZ, spell.Rotation, Module.CastFinishAt(spell), actorID: caster.InstanceID));
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.OID == (uint)OID.GraceBeforeTheFall)
            _aoes.RemoveAll(a => a.ActorID == actor.InstanceID);
    }

    public override void Update() => _aoes.RemoveAll(a => WorldState.CurrentTime > a.Activation.AddSeconds(2d));
}

sealed class CubeRay(BossModule module) : Components.SimpleAOEs(module, (uint)AID.CubeRay, new AOEShapeRect(10f, 10f, 10f))
{
    private static readonly WDir[] offsets = [new(-5f, 0), new(5f, 0), new(0, -5f), new(0, 5f)];
    private readonly HashSet<ulong> _resolved = [];
    private readonly List<AOEInstance> _displayed = [];
    private readonly List<AOEInstance> _cleared = [];
    private AOEInstance[] _firstWave = [], _lastWave = [];
    private readonly Dictionary<ulong, (int stage, WPos point)> _destinations = [];
    private DateTime _lastSpawn;
    private bool _ready;

    public override void AddHints(int slot, Actor actor, TextHints hints) { }

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.MagitekSupport && !actor.IsTargetable && !_resolved.Contains(actor.InstanceID))
        {
            if (_ready && Casters.Count == 0)
            {
                _ready = false;
                _cleared.Clear();
                _destinations.Clear();
                _firstWave = _lastWave = [];
            }
            _lastSpawn = WorldState.CurrentTime;
            SetAOE(actor, WorldState.FutureTime(11d));
        }
    }

    private void SetAOE(Actor caster, DateTime activation)
    {
        Casters.RemoveAll(a => a.ActorID == caster.InstanceID);
        Casters.Add(new(Shape, caster.Position.Quantized(), caster.Rotation, activation, actorID: caster.InstanceID));
        SortHelpers.SortAOEByActivation(Casters);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction && !_resolved.Contains(caster.InstanceID))
            SetAOE(caster, Module.CastFinishAt(spell));
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _displayed.Clear();
        if (Casters.Count == 0)
            return [];
        var first = Casters[0].Activation;
        var next = Casters.FindIndex(a => a.Activation > first.AddSeconds(0.75d));
        var nextDeadline = next >= 0 ? Casters[next].Activation.AddSeconds(0.75d) : first.AddSeconds(0.75d);
        // Show entire waves, never a fixed number of actors: later variants have four supports per wave.
        // Only the earliest wave is forbidden now; forbidding all three leaves no route through resolved squares.
        foreach (var pending in Casters)
        {
            if (pending.Activation > nextDeadline)
                break;
            var aoe = pending;
            aoe.Risky = aoe.Activation <= first.AddSeconds(0.75d);
            aoe.Color = aoe.Risky ? Colors.Danger : Colors.AOE;
            _displayed.Add(aoe);
        }
        // Paint the imminent wave last where two adjacent 20y squares overlap.
        _displayed.Sort((a, b) => b.Activation.CompareTo(a.Activation));
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void DrawArenaBackground(int pcSlot, Actor pc)
    {
        var imminent = new List<Shape>();
        foreach (ref readonly var aoe in ActiveAOEs(pcSlot, pc))
        {
            if (aoe.Risky)
                imminent.Add(new Rectangle(aoe.Origin, 10f, 10f, aoe.Rotation));
            else
                aoe.Shape.Outline(Arena, aoe.Origin, aoe.Rotation, Colors.AOE);
        }
        // Fill only the imminent wave, once. Later waves must not cover the waiting side,
        // and overlapping supports in the same wave must not accumulate opaque fills.
        if (imminent.Count > 0)
            new AOEShapeCustom(imminent).Draw(Arena, Arena.Center, default, Colors.Danger);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell) { } // retain until the actual damage event

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID != WatchedAction || !_resolved.Add(caster.InstanceID))
            return;
        var index = Casters.FindIndex(a => a.ActorID == caster.InstanceID);
        if (index >= 0)
        {
            _cleared.Add(Casters[index]);
            Casters.RemoveAt(index);
        }
        ++NumCasts;
    }

    public override void OnActorDestroyed(Actor actor) => Casters.RemoveAll(a => a.ActorID == actor.InstanceID);

    public override void Update() => Casters.RemoveAll(a => WorldState.CurrentTime > a.Activation.AddSeconds(2d));

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // Until the complete formation is known, do not steer toward whichever wave happens to be latest so far.
        if (!FormationReady() || Casters.Count == 0)
            return;
        base.AddAIHints(slot, actor, assignment, hints);
        if (Destination(actor) is not { } goal)
            return;

        // This is a mechanic destination, rather than a weak uptime preference. Go immediately and hold
        // on the THIRD wave's side until every first-wave damage event has actually arrived.
        hints.AddForbiddenZone(new SDInvertedCircle(goal, 1.5f));
        hints.GoalZones.Add(AIHints.GoalSingleTarget(goal, 1f, 100f));
        hints.MaxCastTime = 0;
        hints.ForceCancelCast = true;
    }

    private bool FormationReady()
    {
        if (_ready)
            return true;
        if (Casters.Count < 9 || WorldState.CurrentTime < _lastSpawn.AddSeconds(0.25d))
            return false;
        var waves = new List<List<AOEInstance>>();
        foreach (var aoe in Casters)
        {
            if (waves.Count == 0 || aoe.Activation > waves[^1][0].Activation.AddSeconds(0.75d))
                waves.Add([]);
            waves[^1].Add(aoe);
        }
        if (waves.Count != 3 || waves[0].Count < 3 || waves[0].Count != waves[1].Count || waves[0].Count != waves[2].Count)
            return false;
        _firstWave = [.. waves[0]];
        _lastWave = [.. waves[2]];
        _ready = true;
        return true;
    }

    private WPos? Destination(Actor actor)
    {
        if (!FormationReady() || Casters.Count == 0)
            return null;
        var firstResolved = _firstWave.All(a => _resolved.Contains(a.ActorID));
        var stage = firstResolved ? _resolved.Count : 0;
        if (_destinations.TryGetValue(actor.InstanceID, out var cached) && cached.stage == stage)
            return cached.point;

        var first = Casters[0].Activation;
        var reference = firstResolved ? _firstWave : _lastWave;
        // On the third side avoid BOTH earlier waves; after the first resolves only the imminent wave
        // constrains entry into its cleared squares. This permits the intermediate step through wave one.
        var avoid = Casters.Where(a => firstResolved ? a.Activation <= first.AddSeconds(0.75d)
            : !_lastWave.Any(last => last.ActorID == a.ActorID)).Select(a => a.Shape.Distance(a.Origin, a.Rotation)).ToArray();
        WPos? destination = null;
        var best = float.MaxValue;
        foreach (var cell in reference)
        {
            foreach (var offset in offsets)
            {
                var p = cell.Origin + offset;
                if (!p.InSquare(Arena.Center, CE61LeanMeanMagitekMachines.SafeHalfSize - 2f)
                    || avoid.Any(a => a.Distance(p) < 2f))
                    continue;
                var distance = (p - actor.Position).LengthSq();
                if (distance < best)
                {
                    best = distance;
                    destination = p;
                }
            }
        }
        if (destination is { } safe)
            _destinations[actor.InstanceID] = (stage, safe);
        return destination;
    }
}

sealed class MagitekWildfire(BossModule module) : Components.GenericRotatingAOE(module)
{
    private static readonly AOEShapeRect rect = new(60f, 4f);
    private readonly Dictionary<ulong, Angle> _increments = [];
    private readonly Dictionary<ulong, (WPos origin, Angle rotation, Angle increment, int remaining, AOEShape shape, ShapeDistance distance)> _sweeps = [];
    private readonly List<AOEInstance> _displayed = [];
    private readonly Dictionary<ulong, WPos> _safePoints = [];

    public override void AddHints(int slot, Actor actor, TextHints hints) { }

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (actor.OID != (uint)OID.DeathMachine || iconID is not ((uint)IconID.RotateCW or (uint)IconID.RotateCCW))
            return;
        var increment = (iconID == (uint)IconID.RotateCW ? -10f : 10f).Degrees();
        _increments[actor.InstanceID] = increment;
        for (var i = 0; i < Sequences.Count; ++i)
            if (Sequences[i].ActorID == actor.InstanceID)
                Sequences.Ref(i).Increment = increment;
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster.OID != (uint)OID.DeathMachine)
            return;
        if (spell.Action.ID is (uint)AID.MagitekWildfireFirst or (uint)AID.MagitekWildfireFirstAlt)
        {
            Sequences.RemoveAll(s => s.ActorID == caster.InstanceID);
            _safePoints.Clear();
            Sequences.Add(new(rect, spell.LocXZ, spell.Rotation, _increments.GetValueOrDefault(caster.InstanceID), Module.CastFinishAt(spell), 1.43d, 10, 1, caster.InstanceID));
        }
        else if (spell.Action.ID == (uint)AID.MagitekWildfireRest)
        {
            var index = Sequences.FindIndex(s => s.ActorID == caster.InstanceID);
            if (index >= 0)
            {
                ref var s = ref Sequences.Ref(index);
                // NPC rotation lags behind the damage direction; use the cast packet's rotation.
                if (s.Increment == default && !(spell.Rotation - s.Rotation).AlmostEqual(default, 0.01f))
                    s.Increment = ((spell.Rotation - s.Rotation).Normalized().Rad > 0 ? 10f : -10f).Degrees();
                s.Origin = spell.LocXZ;
                s.Rotation = spell.Rotation;
                s.NextActivation = Module.CastFinishAt(spell);
            }
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster.OID == (uint)OID.DeathMachine && spell.Action.ID is (uint)AID.MagitekWildfireFirst or (uint)AID.MagitekWildfireFirstAlt or (uint)AID.MagitekWildfireRest)
            AdvanceSequence(caster.InstanceID, WorldState.CurrentTime);
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _displayed.Clear();
        foreach (var s in Sequences)
        {
            if (s.NumRemainingCasts <= 1 || s.Increment == default)
                continue;
            if (!_sweeps.TryGetValue(s.ActorID, out var cached) || cached.origin != s.Origin || cached.rotation != s.Rotation || cached.increment != s.Increment || cached.remaining != s.NumRemainingCasts)
            {
                var end = s.Rotation + (s.NumRemainingCasts - 1) * s.Increment;
                var startAngle = s.Increment.Rad > 0 ? s.Rotation : end;
                var endAngle = s.Increment.Rad > 0 ? end : s.Rotation;
                // Existing cone + rectangle geometry covers the continuous sweep, including the 4y-wide outer edges.
                var shape = new AOEShapeCustom([new Cone(s.Origin, 60f, startAngle, endAngle),
                    new RectangleSE(s.Origin, s.Origin + 60f * s.Rotation.ToDirection(), 4f),
                    new RectangleSE(s.Origin, s.Origin + 60f * end.ToDirection(), 4f)]);
                // Keep the reference drawing, but do not use its clipped polygon for AI distances.
                // The south-edge variant in the 2026-10-24 replay triggers PolygonBoundaryIndex2D.Build
                // on that polygon. The union of the same analytic primitives avoids the failing index.
                var halfAngle = (endAngle - startAngle) * 0.5f;
                var distance = new SDUnion([new SDCone(s.Origin, 60f, startAngle + halfAngle, halfAngle),
                    rect.Distance(s.Origin, s.Rotation), rect.Distance(s.Origin, end)]);
                cached = (s.Origin, s.Rotation, s.Increment, s.NumRemainingCasts, shape, distance);
                _sweeps[s.ActorID] = cached;
            }
            // The remaining sweep is also forbidden to AI, so the intersection of both machines' safe areas is found early.
            _displayed.Add(new(cached.shape, Arena.Center, activation: s.NextActivation, color: Colors.AOE, shapeDistance: cached.distance));
        }
        foreach (var s in Sequences)
            _displayed.Add(new(rect, s.Origin, s.Rotation, s.NextActivation, Colors.Danger, actorID: s.ActorID));
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var aoes = ActiveAOEs(slot, actor);
        if (aoes.Length == 0)
        {
            _safePoints.Clear();
            return;
        }
        // The pale area is a route reservation, not a later cast to greed. Forbid the ENTIRE remaining
        // sweep now, even while the initial beam is still casting, so movement cannot wait for its deadline.
        foreach (ref readonly var aoe in aoes)
            hints.AddForbiddenZone(aoe.ShapeDistance ?? aoe.Shape.Distance(aoe.Origin, aoe.Rotation));
        if (SafePoint(slot, actor) is { } goal)
        {
            hints.AddForbiddenZone(new SDInvertedCircle(goal, 1.5f));
            hints.GoalZones.Add(AIHints.GoalSingleTarget(goal, 1f, 100f));
        }
        hints.MaxCastTime = 0;
        hints.ForceCancelCast = true;
    }

    private WPos? SafePoint(int slot, Actor actor)
    {
        if (Sequences.Count < 2 || Sequences.Any(s => s.Increment == default))
            return null;
        var aoes = ActiveAOEs(slot, actor);
        var distances = new ShapeDistance[aoes.Length];
        for (var i = 0; i < aoes.Length; ++i)
            distances[i] = aoes[i].ShapeDistance ?? aoes[i].Shape.Distance(aoes[i].Origin, aoes[i].Rotation);
        if (_safePoints.TryGetValue(actor.InstanceID, out var cached) && distances.All(d => d.Distance(cached) >= 2f))
            return cached;

        WPos? safe = null;
        var best = float.MinValue;
        // Reuse existing signed distances to select a point inside the intersection of all safe regions.
        // Leave a real margin for character radius and packet/position latency, rather than hugging a beam.
        for (var x = -18; x <= 18; x += 2)
            for (var z = -18; z <= 18; z += 2)
            {
                var p = Arena.Center + new WDir(x, z);
                var clearance = Math.Min(CE61LeanMeanMagitekMachines.SafeHalfSize - Math.Max(Math.Abs(x), Math.Abs(z)), distances.Min(d => d.Distance(p)));
                if (clearance < 2f)
                    continue;
                var score = clearance - 0.01f * (p - actor.Position).Length();
                if (score > best)
                {
                    best = score;
                    safe = p;
                }
            }
        if (safe is { } destination)
            _safePoints[actor.InstanceID] = destination;
        return safe;
    }

    public override void OnActorDestroyed(Actor actor) => Sequences.RemoveAll(s => s.ActorID == actor.InstanceID);
    public override void Update() => Sequences.RemoveAll(s => WorldState.CurrentTime > s.NextActivation.AddSeconds(3d));
}

sealed class ForeAndAft(BossModule module) : Components.GenericRotatingAOE(module)
{
    public override void AddHints(int slot, Actor actor, TextHints hints) { }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.ForeFirst or (uint)AID.AftFirst)
        {
            Sequences.RemoveAll(s => s.ActorID == caster.InstanceID);
            Sequences.Add(new(new AOEShapeRect(spell.Action.ID == (uint)AID.ForeFirst ? 21f : 22f, 5f), spell.LocXZ,
                spell.Rotation, default, Module.CastFinishAt(spell), 1.08d, 7, 1, caster.InstanceID));
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.ForeFirst or (uint)AID.AftFirst or (uint)AID.ForeRest or (uint)AID.AftRest)
            AdvanceSequence(caster.InstanceID, WorldState.CurrentTime);
    }

    public override void OnActorDestroyed(Actor actor) => Sequences.RemoveAll(s => s.ActorID == actor.InstanceID);

    public override void Update()
    {
        Sequences.RemoveAll(s => WorldState.CurrentTime > s.NextActivation.AddSeconds(3d));
        base.Update();
    }
}

sealed class Napalm(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Napalm, 8f)
{
    public override void AddHints(int slot, Actor actor, TextHints hints) { }
}

sealed class HighPoweredMagitekRay(BossModule module) : Components.SimpleAOEs(module, (uint)AID.HighPoweredMagitekRay, new AOEShapeRect(60f, 3f))
{
    public override void AddHints(int slot, Actor actor, TextHints hints) { }
}

sealed class CE61LeanMeanMagitekMachinesStates : StateMachineBuilder
{
    public CE61LeanMeanMagitekMachinesStates(CE61LeanMeanMagitekMachines module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<ElectricFence>()
            .ActivateOnEnter<GraceBeforeTheFall>()
            .ActivateOnEnter<CubeRay>()
            .ActivateOnEnter<MagitekWildfire>()
            .ActivateOnEnter<ForeAndAft>()
            .ActivateOnEnter<Napalm>()
            .ActivateOnEnter<HighPoweredMagitekRay>()
            // Same lifecycle as CE23: losing the first small enemy must not end reinforcement phases.
            .Raw.Update = () => module.Finished;
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed, Contributors = "COXOIDA", PrimaryActorOID = (uint)OID.MagitekInfantry,
    GroupType = BossModuleInfo.GroupType.CriticalEngagement, GroupID = 778, NameID = 27)]
public sealed class CE61LeanMeanMagitekMachines(WorldState ws, Actor primary) : BossModule(ws, primary, arena.Center, arena)
{
    // b2262yuka8_d.avfx has square floor meshes from 20y to 30y, rather than CE64's 20y to 25y.
    public const float ArenaHalfSize = 30f, SafeHalfSize = 20f;
    public static readonly WPos ArenaCenter = new(-310f, -422f);
    private static readonly ArenaBoundsCustom arena = new([new Rectangle(ArenaCenter, ArenaHalfSize, ArenaHalfSize)]);
    private static readonly uint[] enemies = [(uint)OID.MagitekInfantry, (uint)OID.MagitekSkyArmor, (uint)OID.MagitekHunter,
        (uint)OID.MagitekSupport, (uint)OID.DeathMachine, (uint)OID.Boss];
    private Actor? _boss;
    private Actor? _fence;

    internal bool Finished => _boss?.IsDeadOrDestroyed == true || _fence?.IsDestroyed == true;

    protected override bool CheckPull() => base.CheckPull() && Raid.Player() is { } pc && pc.Position.InSquare(ArenaCenter, ArenaHalfSize);

    protected override void UpdateModule()
    {
        _boss ??= GetActor((uint)OID.Boss);
        _fence ??= GetActor((uint)OID.ElectricFence);
    }

    public override bool ShouldPrioritizeAllEnemies => true;
    public override Actor? GetDefaultTarget(int slot) => Enemies(enemies).FirstOrDefault(a => a.IsTargetable && !a.IsDeadOrDestroyed && Arena.InBounds(a.Position));
    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actors(this, enemies);
}
