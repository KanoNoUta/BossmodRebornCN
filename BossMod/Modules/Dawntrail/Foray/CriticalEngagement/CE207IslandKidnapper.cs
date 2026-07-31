using BossMod.Dawntrail.Foray.CriticalEngagement;
using static BossMod.Components.GenericKnockback;

namespace BossMod.Dawntrail.Foray.CriticalEngagement.CE207IslandKidnapper;

public enum OID : uint
{
    Boss = 0x4BE1, // R3.2, BNpcName 14505, kidnapper
    Hurricane = 0x4BE2,
    Emitter = 0x4BE3, // wind spirit, spawns on the R16 ring facing center, casts the B953 ice-flower circles
    Anchor = 0x4BE4, // non-targetable arena controller at center
    Helper = 0x233C
}

public enum AID : uint
{
    IdleVisual = 0xB949, // boss->event target, no effects
    AutoAttack = 0xB94A, // boss->player, no cast, single-target
    WindBoundary = 0xB94B, // anchor, persistent 20-30y outer deathwall
    HurricaneVisual = 0xB94C,
    HurricaneKnockback = 0xB94D, // 5y away knockback
    RendingWindVisual = 0xB94E,
    RendingWind = 0xB94F, // range 60, 8y wide cross; two rotated crosses form the eight-way pattern
    GustHit = 0xB950, // raidwide damage and 24y forward knockback
    GaleBlade = 0xB951, // 60y 180-degree cone
    ScatterFeathers = 0xB952,
    WindBloom = 0xB953, // emitter self-centered 13y circle; four-six of them ring the arena and rotate wave to wave into a moving "ice flower", safe pocket near dead center
    DispersingGalesVisual = 0xB954,
    DispersingGales = 0xB955, // 60y 60-degree cone
    DownburstVisual = 0xB956,
    CycloneRingVisual = 0xB957,
    Downburst = 0xB958, // location, 15y circle
    CycloneRing = 0xB959, // 5-60y donut
    HurricaneHit = 0xBBF8, // helpers, no cast, raidwide damage
    GustTelegraph = 0xBC7A // helper, 60y long, 60y wide rect
}

sealed class WindBoundary(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeDonut Shape = new(19f, 30f);
    private readonly AOEInstance[] _aoe = [new(Shape, module.Arena.Center)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _aoe;
}

sealed class KidnapperAOEs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCone Half = new(60f, 90f.Degrees());
    private static readonly AOEShapeCone Cone = new(60f, 30f.Degrees());
    private static readonly AOEShapeCross Rending = new(60f, 4f);
    private static readonly AOEShapeCircle Downburst = new(15f);
    private static readonly AOEShapeCircle Bloom = new(13f);
    private static readonly AOEShapeDonut Ring = new(5f, 60f);

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.GaleBlade => new(Half),
        (uint)AID.DispersingGales => new(Cone),
        (uint)AID.RendingWind => new(Rending),
        (uint)AID.WindBloom => new(Bloom),
        (uint)AID.Downburst => new(Downburst, true),
        (uint)AID.CycloneRing => new(Ring),
        _ => null
    };
}

// GenericKnockback only renders displacement and does not add an AI forbidden zone. The moving
// hurricane body is itself the four-yalm contact AOE, so publish a slightly padded live hazard too.
sealed class HurricaneHazards(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCircle Shape = new(4.5f);
    private readonly List<AOEInstance> _displayed = [with(8)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _displayed.Clear();
        foreach (var hurricane in Module.Enemies((uint)OID.Hurricane))
            if (!hurricane.IsDeadOrDestroyed)
                _displayed.Add(new(Shape, hurricane.Position, color: Colors.Danger, actorID: hurricane.InstanceID,
                    shapeDistance: Shape.Distance(hurricane.Position, default)));
        return CollectionsMarshal.AsSpan(_displayed);
    }
}

// Hurricanes are persistent actors rather than cast bars. Their B94D contact event applies a
// five-yalm radial knockback inside the four-yalm hit area, so keep the live actor position.
sealed class HurricaneKnockbacks(BossModule module) : Components.GenericKnockback(module)
{
    // Contact is four yalms, but a warning only drawn inside the contact circle is invisible until
    // the player is already being knocked. Use a wider preview radius so the arrow appears as the
    // moving storm approaches; the separate HurricaneHazards circle still marks the lethal body.
    private static readonly AOEShapeCircle Shape = new(10f);
    private readonly List<Knockback> _displayed = [with(8)];

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        _displayed.Clear();
        foreach (var hurricane in Module.Enemies((uint)OID.Hurricane))
            if (!hurricane.IsDeadOrDestroyed)
                _displayed.Add(new(hurricane.Position, 5f, WorldState.FutureTime(0.25d), Shape, default, Kind.AwayFromOrigin, actorID: hurricane.InstanceID));
        return CollectionsMarshal.AsSpan(_displayed);
    }
}

// BC7A is the cast-bar telegraph for B950, whose action effect is a 24y SourceForward knockback.
sealed class GustKnockback(BossModule module) : Components.SimpleKnockbacks(module, (uint)AID.GustTelegraph, 24f, shape: new AOEShapeRect(60f, 30f), kind: Kind.DirForward);
// B94C resolves into the BBF8 helper raidwide about 0.9s after the boss cast. BC7A similarly
// resolves into B950 while applying the directional knockback.
sealed class KidnapperRaidwides(BossModule module) : Components.RaidwideCasts(module, [(uint)AID.HurricaneVisual, (uint)AID.GustTelegraph]);

sealed class IslandKidnapperStates : StateMachineBuilder
{
    public IslandKidnapperStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<WindBoundary>()
            .ActivateOnEnter<KidnapperAOEs>()
            .ActivateOnEnter<HurricaneHazards>()
            .ActivateOnEnter<HurricaneKnockbacks>()
            .ActivateOnEnter<GustKnockback>()
            .ActivateOnEnter<KidnapperRaidwides>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(IslandKidnapperStates),
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Contributors = "KanoNoUta",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CriticalEngagement,
    GroupID = 1093u,
    NameID = 61u,
    SortOrder = 6)]
public sealed class IslandKidnapper(WorldState ws, Actor primary) : BossModule(ws, primary, new(-150f, -860f), new ArenaBoundsCircle(20f));
