using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.Foray.CriticalEngagement.CE210AcceptNoImitators;

public enum OID : uint
{
    Boss = 0x4C77, // R3.0, BNpcName 14801, morphing mage
    BoundaryController = 0x4DFD, // non-targetable controller at arena center
    Helper = 0x233C
}

public enum AID : uint
{
    AutoAttack = 0xBCCE,
    BlackenedRainVisual = 0xBCCF, // boss->self, 4.0s cast, raidwide visual
    BlackenedRain = 0xBCD0, // three helpers->players, 5.0s cast, raidwide damage
    DarkDealing = 0xBCD1, // boss->player, 5.0s cast, tankbuster
    ChangeFire = 0xBCD2, // boss->self, 4.0s cast, visual
    ChangeWind = 0xBCD3, // boss->self, 4.0s cast, visual
    Revert = 0xBCD4, // boss->self, no cast, visual
    TongueOfFlame = 0xBCD5, // boss->self, 4.0s cast, range 15 circle
    HellfireFetchVisual = 0xBCD6,
    HellwardBound = 0xBCD7, // boss->location, 6.0s cast, visual
    HellwardBoundHit = 0xBCD8,
    HellfireFetch = 0xBCD9, // helper->location, 7.0s cast, range 6 circle
    HellishBreathVisual = 0xBCDA, // boss->self, 6.0s cast, visual
    HellishBreathShort = 0xBCDB, // helper->self, 2.0s cast, range 60 60-degree cone
    HellishBreathMedium = 0xBCDC, // helper->self, 4.0s cast, range 60 60-degree cone
    HellishBreathLong = 0xBCDD, // helper->self, 6.0s cast, range 60 60-degree cone
    HellishBreathHit1 = 0xBCDE,
    HellishBreathHit2 = 0xBCDF,
    HellishBreathHit3 = 0xBCE0,
    HellishBreathHit4 = 0xBCE1,
    CyclonicRing = 0xBCE2, // boss->self, 4.0s cast, range 10-30 donut
    ShapeshiftingSupercellVisual1 = 0xBCE3, // boss->self, 5.5s cast, visual
    ShapeshiftingSupercellVisual2 = 0xBCE4, // boss->self, 5.5s cast, visual
    ShapeshiftingSupercellConeLong = 0xBCE5, // helper->self, 6.0s cast, range 60 90-degree cone
    ShapeshiftingSupercellResolve = 0xBCE6,
    ShapeshiftingSupercellConeShort = 0xBCE7, // helper->self, 1.5s cast, range 60 90-degree cone
    ShapeshiftingSupercellCircle = 0xBCE8, // helper->self, 6.0s cast, range 8 circle
    ShapeshiftingSupercellDonutInner = 0xBCE9, // helper->self, 6.0s cast, range 10-16 donut
    ShapeshiftingSupercellDonutOuter = 0xBCEA, // helper->self, 6.0s cast, range 16-30 donut
    MadeMagicVisual = 0xBCEB, // boss->self, 4.0s cast, visual
    MadeMagic = 0xBCEC, // helper pulses; radius is modified by status 1909
    CycloneCrossingVisual = 0xBCED, // boss->self, 10.5s cast, visual
    CycloneCrossing = 0xBCEE, // helper->self, 11.5s cast, range 60 width 16 cross
    LethalBoundary = 0xBCEF, // controller, persistent 20-30 donut
    UnknownTarget1 = 0xBCF0,
    UnknownTarget2 = 0xBCF1,
    UnknownLocation = 0xC620
}

sealed class LethalBoundary(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeDonut Shape = new(20f, 30f);
    private readonly AOEInstance[] _aoe = [new(Shape, module.Arena.Center)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _aoe;
}

// Every avoidable AOE below has an authoritative cast-start packet from the actor that owns the
// shape. The helpers also carry their actual origin/rotation, so none of the patterns are inferred
// from the boss transformation visuals.
sealed class MorphingMageAOEs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCircle Tongue = new(15f);
    private static readonly AOEShapeCircle Hellfire = new(6f);
    private static readonly AOEShapeCone HellishBreath = new(60f, 30f.Degrees());
    private static readonly AOEShapeDonut CyclonicRing = new(10f, 30f);
    private static readonly AOEShapeCone SupercellCone = new(60f, 45f.Degrees());
    private static readonly AOEShapeCircle SupercellCircle = new(8f);
    private static readonly AOEShapeDonut SupercellInner = new(10f, 16f);
    private static readonly AOEShapeDonut SupercellOuter = new(16f, 30f);
    private static readonly AOEShapeCross CycloneCross = new(60f, 8f);

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.TongueOfFlame => new(Tongue),
        (uint)AID.HellfireFetch => new(Hellfire, true),
        (uint)AID.HellishBreathShort or (uint)AID.HellishBreathMedium or (uint)AID.HellishBreathLong => new(HellishBreath),
        (uint)AID.CyclonicRing => new(CyclonicRing),
        (uint)AID.ShapeshiftingSupercellConeLong or (uint)AID.ShapeshiftingSupercellConeShort => new(SupercellCone),
        (uint)AID.ShapeshiftingSupercellCircle => new(SupercellCircle),
        (uint)AID.ShapeshiftingSupercellDonutInner => new(SupercellInner),
        (uint)AID.ShapeshiftingSupercellDonutOuter => new(SupercellOuter),
        (uint)AID.CycloneCrossing => new(CycloneCross),
        _ => null
    };
}

// The three BCD0 helpers split one raidwide across the participant list. The boss cast is the
// stable, non-duplicated warning and starts one second before the helper cast bars.
sealed class BlackenedRain(BossModule module) : Components.RaidwideCast(module, (uint)AID.BlackenedRainVisual);
sealed class DarkDealing(BossModule module) : Components.SingleTargetDelayableCast(module, (uint)AID.DarkDealing);
sealed class HellwardBound(BossModule module) : Components.ChargeAOEs(module, (uint)AID.HellwardBound, 5f);

sealed class AcceptNoImitatorsStates : StateMachineBuilder
{
    public AcceptNoImitatorsStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<LethalBoundary>()
            .ActivateOnEnter<MorphingMageAOEs>()
            .ActivateOnEnter<BlackenedRain>()
            .ActivateOnEnter<DarkDealing>()
            .ActivateOnEnter<HellwardBound>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(AcceptNoImitatorsStates),
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Contributors = "KanoNoUta",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CriticalEngagement,
    GroupID = 1093u,
    NameID = 63u,
    SortOrder = 9)]
public sealed class AcceptNoImitators(WorldState ws, Actor primary) : BossModule(ws, primary, new(500f, -310f), new ArenaBoundsCircle(20f));
