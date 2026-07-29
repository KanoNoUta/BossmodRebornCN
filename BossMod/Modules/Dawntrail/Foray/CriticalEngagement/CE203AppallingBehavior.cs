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
    ElectricBoundary = 0xC26B, // anchor, persistent outer deathwall; observed hits at 18.6-24.4y
    Summon = 0xC26C,
    EsotericInstruction = 0xC26D,
    EsotericInstructionReverse = 0xC26E,
    ReversePolarity = 0xC26F,

    BadBreathInstruction = 0xC271, // helper, 50y 100-degree cone
    PlaincrackerInstruction = 0xC273, // helper, 30y circle

    Roulette = 0xC27B,
    RouletteCenter = 0xC27C,
    RouletteInner = 0xC27D,
    RouletteOuter = 0xC27E,

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

// The controller at arena center pulses the electric deathwall throughout the encounter. The
// recorded victims were 18.6-24.4y from center, so expose the dangerous outer two yalms
// instead of relying on the arena outline alone.
sealed class ElectricBoundary(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeDonut Shape = new(18f, 25f);
    private readonly AOEInstance[] _aoe = [new(Shape, module.Arena.Center)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _aoe;
}

sealed class AppallingAOEs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCone BadBreath = new(50f, 50f.Degrees());
    private static readonly AOEShapeCircle PlaincrackerLarge = new(30f);
    private static readonly AOEShapeCircle PlaincrackerSmall = new(15f);
    private static readonly AOEShapeCone Lilliputian = new(40f, 90f.Degrees());
    private static readonly AOEShapeCircle Hammer = new(8f);
    private static readonly AOEShapeCircle Missile = new(6f);

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.BadBreathInstruction or (uint)AID.BadBreathAOE => new(BadBreath),
        (uint)AID.PlaincrackerInstruction => new(PlaincrackerLarge),
        (uint)AID.PlaincrackerAOE => new(PlaincrackerSmall),
        (uint)AID.LilliputianLyricAOE => new(Lilliputian),
        (uint)AID.MagicHammerAOE => new(Hammer, true),
        (uint)AID.OccultMissileAOE => new(Missile, true),
        _ => null
    };
}

// The three C512 helper casts each hit the raid; the boss cast is the stable warning packet.
sealed class GreatWhirlwind(BossModule module) : Components.RaidwideCast(module, (uint)AID.GreatWhirlwind);
sealed class DeathRoulette(BossModule module) : Components.CastHint(module, (uint)AID.Roulette, "Death roulette - identify the safe ring");

sealed class AppallingBehaviorStates : StateMachineBuilder
{
    public AppallingBehaviorStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<ElectricBoundary>()
            .ActivateOnEnter<AppallingAOEs>()
            .ActivateOnEnter<DeathRoulette>()
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
