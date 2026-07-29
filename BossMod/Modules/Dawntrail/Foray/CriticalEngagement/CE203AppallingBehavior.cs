using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.Foray.CriticalEngagement.CE203AppallingBehavior;

public enum OID : uint
{
    Boss = 0x4D8F, // R3.0, BNpcName 14714, Pallmagia
    Pallkeeper = 0x4D90, // BNpcName 14715
    Anchor = 0x4D91, // non-targetable Pallmagia controller
    Helper = 0x233C
}

public enum AID : uint
{
    ElectricBoundary = 0xC26B, // anchor, persistent arena-control pulse (not an 18-25y donut)
    Summon = 0xC26C,
    EsotericInstruction = 0xC26D,
    EsotericInstructionReverse = 0xC26E,
    ReversePolarity = 0xC26F,

    BadBreathInstruction = 0xC271, // helper, 50y 100-degree cone
    PlaincrackerInstruction = 0xC273, // helper, 30y circle

    Roulette = 0xC27B,
    RouletteCenter = 0xC27C, // helper, 5y center cell
    RouletteInner = 0xC27D, // helper, 5-12y 60-degree donut sector; two opposite helpers
    RouletteOuter = 0xC27E, // helper, 12-20y 45-degree donut sector; two opposite helpers

    LilliputianLyric = 0xC27F,
    LilliputianLyricAOE = 0xC280, // helper, 40y 180-degree cone
    MagicHammer = 0xC281,
    MagicHammerAOE = 0xC282, // helper->location, 8y circle
    OccultMissile = 0xC283,
    OccultMissileAOE = 0xC285, // helper->location, 6y circle
    GreatWhirlwind = 0xC286,
    GreatWhirlwindVisual = 0xC287,
    GreatWhirlwindHit = 0xC512,

    BadBreath = 0xC53A,
    BadBreathAOE = 0xC53B, // helper, 50y 100-degree cone
    Plaincracker = 0xC53C,
    PlaincrackerAOE = 0xC53D, // helper, 15y circle
    AutoAttack = 0xC53E
}

// Helpers can be teleported (and, after Reverse Polarity, swapped to the opposite keeper) between
// cast-start and effect. Keep the activation from the cast packet, but follow the live helper for
// self-targeted shapes instead of freezing the initial, often deliberately fake, coordinates.
sealed class AppallingAOEs(BossModule module) : Components.GenericAOEs(module)
{
    private readonly record struct AOEConfig(AOEShape Shape, bool LocationTargeted = false);
    private sealed record Pending(uint ActionID, ulong ActorID, AOEShape Shape, WPos Origin, Angle Rotation, DateTime Activation, bool FollowCaster);

    private static readonly AOEShapeCone BadBreath = new(50f, 50f.Degrees());
    private static readonly AOEShapeCircle PlaincrackerLarge = new(30f);
    private static readonly AOEShapeCircle PlaincrackerSmall = new(15f);
    private static readonly AOEShapeCone Lilliputian = new(40f, 90f.Degrees());
    private static readonly AOEShapeCircle Hammer = new(8f);
    private static readonly AOEShapeCircle Missile = new(6f);
    private readonly List<Pending> _pending = [];
    private readonly List<AOEInstance> _displayed = [];
    private readonly HashSet<uint> _seenSequences = [];

    private static AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.BadBreathInstruction or (uint)AID.BadBreathAOE => new(BadBreath),
        (uint)AID.PlaincrackerInstruction => new(PlaincrackerLarge),
        (uint)AID.PlaincrackerAOE => new(PlaincrackerSmall),
        (uint)AID.LilliputianLyricAOE => new(Lilliputian),
        (uint)AID.MagicHammerAOE => new(Hammer, true),
        (uint)AID.OccultMissileAOE => new(Missile, true),
        _ => null
    };

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        Prune();
        _displayed.Clear();
        foreach (var pending in _pending.OrderBy(p => p.Activation))
        {
            var source = pending.FollowCaster ? WorldState.Actors.Find(pending.ActorID) : null;
            var origin = source?.Position ?? pending.Origin;
            var rotation = source?.Rotation ?? pending.Rotation;
            _displayed.Add(new(pending.Shape, origin, rotation, pending.Activation, actorID: pending.ActorID, shapeDistance: pending.Shape.Distance(origin, rotation)));
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update() => Prune();

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (ConfigFor(spell.Action.ID) is not { } config || spell.EventHappened)
            return;

        var activation = Module.CastFinishAt(spell);
        if (activation <= WorldState.CurrentTime)
            return;

        _pending.RemoveAll(p => p.ActionID == spell.Action.ID && p.ActorID == caster.InstanceID);
        var origin = config.LocationTargeted ? spell.LocXZ : caster.Position;
        _pending.Add(new(spell.Action.ID, caster.InstanceID, config.Shape, origin, spell.Rotation, activation, !config.LocationTargeted));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened)
            _pending.RemoveAll(p => p.ActionID == spell.Action.ID && p.ActorID == caster.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (ConfigFor(spell.Action.ID) == null || spell.GlobalSequence != 0 && !_seenSequences.Add(spell.GlobalSequence))
            return;

        _pending.RemoveAll(p => p.ActionID == spell.Action.ID && p.ActorID == caster.InstanceID);
        ++NumCasts;
    }

    public override void OnActorDestroyed(Actor actor) => _pending.RemoveAll(p => p.ActorID == actor.InstanceID);

    private void Prune()
    {
        var now = WorldState.CurrentTime;
        _pending.RemoveAll(p => now > p.Activation.AddSeconds(1d));
    }
}

// Death Roulette is a polar grid, not the C26B 18-25y donut that used to be drawn here. ARR v5
// recordings show five fixed helpers owned by the boss: boss+6 is the center, boss+37/+38 are the
// opposite inner sectors and boss+39/+40 the opposite outer sectors. Their 0x022A movement packets
// update position/rotation immediately before the five consecutive C27C-C27E effects, so build the
// cells from the live actors and clear the whole snapshot atomically after all five sequences.
sealed class DeathRouletteGrid(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCircle CenterCell = new(5f);
    private static readonly AOEShapeDonutSector InnerCell = new(5f, 12f, 30f.Degrees());
    private static readonly AOEShapeDonutSector OuterCell = new(12f, 20f, 22.5f.Degrees());
    private readonly List<AOEInstance> _displayed = [];
    private readonly HashSet<uint> _seenSequences = [];
    private DateTime _activation;
    private int _resolvedCells;
    private bool _armed;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        Prune();
        _displayed.Clear();
        if (!_armed)
            return CollectionsMarshal.AsSpan(_displayed);

        var inner1 = Helper(37);
        var inner2 = Helper(38);
        var outer1 = Helper(39);
        var outer2 = Helper(40);
        // Spawn coordinates are intentionally parked at the boss. Only publish the grid once all
        // four sector helpers have received their center movement packet; this avoids fake safespots.
        if (inner1 == null || inner2 == null || outer1 == null || outer2 == null ||
            (inner1.Position - Module.Arena.Center).LengthSq() > 1f || (inner2.Position - Module.Arena.Center).LengthSq() > 1f ||
            (outer1.Position - Module.Arena.Center).LengthSq() > 1f || (outer2.Position - Module.Arena.Center).LengthSq() > 1f)
            return CollectionsMarshal.AsSpan(_displayed);

        Add(CenterCell, default);
        Add(InnerCell, inner1.Rotation, inner1.InstanceID);
        Add(InnerCell, inner2.Rotation, inner2.InstanceID);
        Add(OuterCell, outer1.Rotation, outer1.InstanceID);
        Add(OuterCell, outer2.Rotation, outer2.InstanceID);
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void AddGlobalHints(GlobalHints hints)
    {
        if (_armed)
            hints.Add("Death roulette: watch the polar grid");
    }

    public override void Update() => Prune();

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.Roulette && !spell.EventHappened)
            Arm(Module.CastFinishAt(spell, 14.4f));
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.Roulette)
        {
            if (!_armed)
                Arm(WorldState.FutureTime(14.4d));
            return;
        }

        if (spell.Action.ID is < (uint)AID.RouletteCenter or > (uint)AID.RouletteOuter || spell.GlobalSequence != 0 && !_seenSequences.Add(spell.GlobalSequence))
            return;

        if (++_resolvedCells >= 5)
            Clear();
        ++NumCasts;
    }

    private Actor? Helper(ulong offset)
    {
        var actor = WorldState.Actors.Find(Module.PrimaryActor.InstanceID + offset);
        return actor?.OID == (uint)OID.Helper ? actor : null;
    }

    private void Add(AOEShape shape, Angle rotation, ulong actorID = 0)
        => _displayed.Add(new(shape, Module.Arena.Center, rotation, _activation, actorID: actorID, shapeDistance: shape.Distance(Module.Arena.Center, rotation)));

    private void Arm(DateTime activation)
    {
        _armed = true;
        _activation = activation;
        _resolvedCells = 0;
        _seenSequences.Clear();
    }

    private void Clear()
    {
        _armed = false;
        _resolvedCells = 0;
        _displayed.Clear();
    }

    private void Prune()
    {
        if (_armed && WorldState.CurrentTime > _activation.AddSeconds(1d))
            Clear();
    }
}

// The three C512 helper casts each hit the raid; the boss cast is the stable warning packet.
sealed class GreatWhirlwind(BossModule module) : Components.RaidwideCast(module, (uint)AID.GreatWhirlwind);

sealed class AppallingBehaviorStates : StateMachineBuilder
{
    public AppallingBehaviorStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<AppallingAOEs>()
            .ActivateOnEnter<DeathRouletteGrid>()
            .ActivateOnEnter<GreatWhirlwind>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(AppallingBehaviorStates),
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Contributors = "KanoNoUta",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CriticalEngagement,
    GroupID = 1093u,
    NameID = 59u,
    SortOrder = 2)]
public sealed class AppallingBehavior(WorldState ws, Actor primary) : BossModule(ws, primary, new(807f, -562f), new ArenaBoundsCircle(20f))
{
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.Pallkeeper));
    }
}
