namespace BossMod.Dawntrail.Foray.CriticalEngagement.CE201FamiliarTactics;

public enum OID : uint
{
    Boss = 0x4B12, // R2.5, BNpcName 14508 (elm gigas)
    AlabasterBlade = 0x4B13, // R1.25, moving persistent hazard
    AlabasterBladeVariant = 0x4B18, // R1.25, same blade model; observed BNpcBase variant
    AlabasterBladeLarge = 0x4C3E, // R2.0, same blade model; observed BNpcBase variant
    Helper = 0x233C
}

public enum AID : uint
{
    HyperconductivePlasma = 47528, // Boss->self, 5.0s cast, raidwide
    BatteringArms = 47529, // Boss->self, 6.0s cast, tankbuster visual

    UnbowedSpiritVisual = 47530, // Boss->self, 3.0s cast, summons moving blades
    UnbowedSpirit = 47531, // blade->self, no cast, range 4 circle

    InspiritedCycloneVisual = 47532, // Boss->self, 5.0s cast, single-target visual
    InspiritedCrosswindsVisual = 47533, // Boss->self, 6.0s cast, single-target visual
    InspiritedCyclone = 47534, // blade/helper->self, 6.0s cast, range 12 circle
    InspiritedCrosswinds = 47535, // blade/helper->self, 6.0s cast, range 60 width 8 cross

    InspiritedHurricaneVisual = 47536, // Boss->self, 4.3s cast, single-target visual
    InspiritedHurricaneCircle = 47537, // blade/helper->self, 5.0s cast, range 12 circle
    InspiritedHurricaneCross = 47538, // blade/helper->self, 5.0s cast, range 60 width 10 cross
    Gale = 47539, // blade->self, no cast, range 4 circle

    AncientAero = 47540, // blade/helper->self, 3.0s cast, range 70 width 6 rect
    SpinningSweep = 47541, // Boss->self, 6.0s cast, range 40 120-degree cone

    InspiritedImpactVisual = 47542, // Boss->self, 3.0s cast, single-target visual
    InspiritedImpact = 47543 // helper->self, 9.6s cast, range 25 circle
}

sealed class HyperconductivePlasma(BossModule module) : Components.RaidwideCast(module, (uint)AID.HyperconductivePlasma);
sealed class BatteringArms(BossModule module) : Components.SingleTargetDelayableCast(module, (uint)AID.BatteringArms);
sealed class SpinningSweep(BossModule module) : Components.SimpleAOEs(module, (uint)AID.SpinningSweep, new AOEShapeCone(40f, 60f.Degrees()));

// The blades remain dangerous while travelling. Their no-cast action effects (47531/47539)
// only report contact after it happened, so the live actor positions are the useful warning.
sealed class UnbowedSpirit(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCircle Shape = new(4f);
    private readonly List<Actor>[] _bladeGroups =
    [
        module.Enemies((uint)OID.AlabasterBlade),
        module.Enemies((uint)OID.AlabasterBladeVariant),
        module.Enemies((uint)OID.AlabasterBladeLarge)
    ];
    private readonly HashSet<ulong> _observedBladeIDs = [];
    private readonly List<AOEInstance> _active = [with(8)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _active.Clear();
        foreach (var group in _bladeGroups)
        {
            foreach (var blade in group)
            {
                AddBlade(blade);
            }
        }
        foreach (var instanceID in _observedBladeIDs)
        {
            if (WorldState.Actors.Find(instanceID) is { } blade && !_bladeGroups.Any(group => group.Any(known => known.InstanceID == instanceID)))
            {
                AddBlade(blade);
            }
        }
        return CollectionsMarshal.AsSpan(_active);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.UnbowedSpirit or (uint)AID.Gale)
        {
            _observedBladeIDs.Add(caster.InstanceID);
        }
    }

    public override void OnActorDeath(Actor actor) => _observedBladeIDs.Remove(actor.InstanceID);
    public override void OnActorDestroyed(Actor actor) => _observedBladeIDs.Remove(actor.InstanceID);

    private void AddBlade(Actor blade)
    {
        if (!blade.IsDeadOrDestroyed)
        {
            var origin = blade.Position;
            _active.Add(new(Shape, origin, actorID: blade.InstanceID, shapeDistance: Shape.Distance(origin, default)));
        }
    }
}

// All blade patterns are driven by real helper cast-start packets. In particular, cross AOEs
// must not be predicted from the boss visual: the moving blades can stop at arbitrary positions
// and rotations. Track action + instance + activation so duplicate/late packets cannot remove a
// different blade or a later wave from the same caster.
sealed class BladePatterns(BossModule module) : Components.GenericAOEs(module)
{
    private const double WaveWindow = 0.5d;
    private const double DuplicateWindow = 0.25d;
    private const double ResolveWindow = 2d;
    private const double ExpireDelay = 2d;

    private static readonly AOEShapeCircle Circle12 = new(12f);
    private static readonly AOEShapeCross Cross8 = new(60f, 4f);
    private static readonly AOEShapeCross Cross10 = new(60f, 5f);
    private static readonly AOEShapeRect AncientAeroRect = new(70f, 3f);
    private static readonly AOEShapeCircle ImpactCircle = new(25f);

    private sealed class PendingAOE(uint actionID, AOEInstance aoe)
    {
        public readonly uint ActionID = actionID;
        public AOEInstance AOE = aoe;
    }

    private readonly List<PendingAOE> _pending = [with(16)];
    private readonly List<AOEInstance> _displayed = [with(8)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        if (_pending.Count == 0)
        {
            return [];
        }

        var deadline = _pending[0].AOE.Activation.AddSeconds(WaveWindow);
        foreach (var entry in _pending)
        {
            if (entry.AOE.Activation > deadline)
            {
                break;
            }

            var aoe = entry.AOE;
            aoe.Color = Colors.Danger;
            aoe.Risky = true;
            _displayed.Add(aoe);
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update() => PruneExpired();

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        var shape = ShapeFor(spell.Action.ID);
        if (shape == null)
        {
            return;
        }

        var origin = spell.LocXZ.Quantized();
        var rotation = spell.Rotation;
        AddOrRefresh(spell.Action.ID, shape, caster.InstanceID, origin, rotation, Module.CastFinishAt(spell));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (ShapeFor(spell.Action.ID) != null)
        {
            Resolve(spell.Action.ID, caster);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (ShapeFor(spell.Action.ID) != null)
        {
            ++NumCasts;
        }
    }

    public override void OnActorDeath(Actor actor) => RemoveActor(actor.InstanceID);
    public override void OnActorDestroyed(Actor actor) => RemoveActor(actor.InstanceID);

    private static AOEShape? ShapeFor(uint actionID) => actionID switch
    {
        (uint)AID.InspiritedCyclone or (uint)AID.InspiritedHurricaneCircle => Circle12,
        (uint)AID.InspiritedCrosswinds => Cross8,
        (uint)AID.InspiritedHurricaneCross => Cross10,
        (uint)AID.AncientAero => AncientAeroRect,
        (uint)AID.InspiritedImpact => ImpactCircle,
        _ => null
    };

    private void AddOrRefresh(uint actionID, AOEShape shape, ulong actorID, WPos origin, Angle rotation, DateTime activation)
    {
        var replacement = new AOEInstance(shape, origin, rotation, activation, actorID: actorID, shapeDistance: shape.Distance(origin, rotation));
        foreach (var entry in _pending)
        {
            if (entry.ActionID == actionID && entry.AOE.ActorID == actorID && Math.Abs((entry.AOE.Activation - activation).TotalSeconds) <= DuplicateWindow)
            {
                entry.AOE = replacement;
                SortPending();
                return;
            }
        }

        _pending.Add(new(actionID, replacement));
        SortPending();
    }

    private void Resolve(uint actionID, Actor caster)
    {
        var now = WorldState.CurrentTime;
        var best = FindBest(actionID, now, caster.InstanceID);
        if (best >= 0)
        {
            _pending.RemoveAt(best);
        }
    }

    private int FindBest(uint actionID, DateTime now, ulong actorID)
    {
        var best = -1;
        var bestDelta = ResolveWindow;
        for (var i = 0; i < _pending.Count; ++i)
        {
            var entry = _pending[i];
            if (entry.ActionID != actionID || entry.AOE.ActorID != actorID)
            {
                continue;
            }

            var delta = Math.Abs((entry.AOE.Activation - now).TotalSeconds);
            if (delta <= bestDelta)
            {
                best = i;
                bestDelta = delta;
            }
        }
        return best;
    }

    private void PruneExpired()
    {
        var now = WorldState.CurrentTime;
        _pending.RemoveAll(entry => now > entry.AOE.Activation.AddSeconds(ExpireDelay));
    }

    private void RemoveActor(ulong instanceID) => _pending.RemoveAll(entry => entry.AOE.ActorID == instanceID);
    private void SortPending() => _pending.Sort((left, right) => left.AOE.Activation.CompareTo(right.AOE.Activation));
}

sealed class FamiliarTacticsStates : StateMachineBuilder
{
    public FamiliarTacticsStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<HyperconductivePlasma>()
            .ActivateOnEnter<BatteringArms>()
            .ActivateOnEnter<UnbowedSpirit>()
            .ActivateOnEnter<BladePatterns>()
            .ActivateOnEnter<SpinningSweep>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP,
    StatesType = typeof(FamiliarTacticsStates),
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Contributors = "The Combat Reborn Team",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CriticalEngagement,
    GroupID = 1093u,
    NameID = 58u,
    SortOrder = 0)]
public sealed class FamiliarTactics(WorldState ws, Actor primary) : BossModule(ws, primary, new(-390f, 700f), new ArenaBoundsCircle(20f))
{
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
    }
}
