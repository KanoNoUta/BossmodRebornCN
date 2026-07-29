using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.Foray.CriticalEngagement.CE208AwakenedHydra;

public enum OID : uint
{
    Boss = 0x4BC5, // R3.6, BNpcName 14523, magicked hydra
    Helper = 0x233C
}

public enum AID : uint
{
    ElementalSpillVisual = 0xB850,
    ElementalSpill1 = 0xB851,
    ElementalSpill2 = 0xB852,
    ElementalSpill3 = 0xB853,
    ElementalSpill4 = 0xB854,
    ElementalSpill5 = 0xB855, // helper->location, 6y circles
    RadiantIce = 0xB858, // 40y 20-degree cone
    ToxinScatter = 0xB859,
    Discharge = 0xB85A, // 10y circle
    RingLightningInner = 0xB85B, // 10-20y donut
    RingLightningOuter = 0xB85C, // 20-30y donut
    NearShockwaveVisual = 0xB85D,
    FarShockwaveVisual = 0xB85E,
    ElementalShockwave1 = 0xB85F,
    ElementalShockwave2 = 0xB860,
    ElementalShockwave3 = 0xB861,
    ElementalShockwave4 = 0xB862,
    ElementalShockwave5 = 0xB863, // helper->location, 8y circles
    StarlightBreath = 0xB868,
    QuintetRoar = 0xB869,
    QuintetRoarHit = 0xB86A,
    MultipleBreaths1 = 0xB86C, // 30y 120-degree cone
    MultipleBreathsVisual = 0xB86D,
    MultipleBreaths2 = 0xC5F1,
    MultipleBreaths3 = 0xC5F2,
    MultipleBreaths4 = 0xC5F3
}

sealed class HydraAOEs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCircle Spill = new(6f);
    private static readonly AOEShapeCone Ice = new(40f, 10f.Degrees());
    private static readonly AOEShapeCircle Discharge = new(10f);
    private static readonly AOEShapeDonut InnerRing = new(10f, 20f);
    private static readonly AOEShapeDonut OuterRing = new(20f, 30f);
    private static readonly AOEShapeCircle Shockwave = new(8f);
    private static readonly AOEShapeCone Breath = new(30f, 60f.Degrees());

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        >= (uint)AID.ElementalSpill1 and <= (uint)AID.ElementalSpill5 => new(Spill, true),
        (uint)AID.RadiantIce => new(Ice),
        (uint)AID.Discharge => new(Discharge),
        (uint)AID.RingLightningInner => new(InnerRing),
        (uint)AID.RingLightningOuter => new(OuterRing),
        >= (uint)AID.ElementalShockwave1 and <= (uint)AID.ElementalShockwave5 => new(Shockwave, true),
        (uint)AID.MultipleBreaths1 or (uint)AID.MultipleBreaths2 or (uint)AID.MultipleBreaths3 or (uint)AID.MultipleBreaths4 => new(Breath),
        _ => null
    };
}

// B86A is emitted by several helpers and splits the damage packets; the boss visual is the
// stable cast-bar warning for the unavoidable hit.
sealed class QuintetRoar(BossModule module) : Components.RaidwideCast(module, (uint)AID.QuintetRoar);

sealed class AwakenedHydraStates : StateMachineBuilder
{
    public AwakenedHydraStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<HydraAOEs>()
            .ActivateOnEnter<QuintetRoar>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(AwakenedHydraStates),
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Contributors = "KanoNoUta",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CriticalEngagement,
    GroupID = 1093u,
    NameID = 62u,
    SortOrder = 7)]
public sealed class AwakenedHydra(WorldState ws, Actor primary) : BossModule(ws, primary, new(-82f, 485f), new ArenaBoundsCircle(20f));
