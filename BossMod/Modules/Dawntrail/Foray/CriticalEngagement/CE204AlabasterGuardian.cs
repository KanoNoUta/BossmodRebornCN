using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.Foray.CriticalEngagement.CE204AlabasterGuardian;

public enum OID : uint
{
    Boss = 0x4BBE, // R2.5, BNpcName 14509, Alabaster Blade
    Helper = 0x233C
}

public enum AID : uint
{
    Summon = 0xB832,
    FourfoldCommand = 0xB833,
    AttackCommand = 0xB834,
    SummonOrbs = 0xB837,
    FabricatedHoly = 0xB839, // boss->self, raidwide
    MagicGust = 0xB83B, // helper, 50y long, 10y wide rect
    MagicStone = 0xB83C, // helper, 40y 60-degree cone
    MagicTornado = 0xB83D, // helper->location, 5y circle
    RightLeftSlash = 0xB83E, // boss, 40y 180-degree cone
    LeftRightSlash = 0xB83F, // boss, 40y 180-degree cone
    SweepRight = 0xB840,
    SweepLeft = 0xB841,
    MagicStorm = 0xB842, // helper, 50y long, 10y wide rect
    StoneSwordShockwave = 0xB843, // boss->self, raidwide visual
    StoneSwordShockwaveHit = 0xB844
}

sealed class AlabasterAOEs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeRect Line = new(50f, 5f);
    private static readonly AOEShapeCone Stone = new(40f, 30f.Degrees());
    private static readonly AOEShapeCircle Tornado = new(5f);

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.MagicGust or (uint)AID.MagicStorm => new(Line),
        (uint)AID.MagicStone => new(Stone),
        (uint)AID.MagicTornado => new(Tornado, true),
        _ => null
    };
}

sealed class AlabasterSlashes(BossModule module) : ReplayValidatedOppositeAOEs(module)
{
    private static readonly AOEShapeCone Half = new(40f, 90f.Degrees());

    protected override SequenceConfig? ConfigFor(uint firstActionID) => firstActionID switch
    {
        (uint)AID.RightLeftSlash => new(Half, Half, (uint)AID.SweepRight, 1.7d),
        (uint)AID.LeftRightSlash => new(Half, Half, (uint)AID.SweepLeft, 1.7d),
        _ => null
    };
}

sealed class AlabasterRaidwides(BossModule module) : Components.RaidwideCasts(module, [(uint)AID.FabricatedHoly, (uint)AID.StoneSwordShockwave]);

sealed class AlabasterGuardianStates : StateMachineBuilder
{
    public AlabasterGuardianStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<AlabasterAOEs>()
            .ActivateOnEnter<AlabasterSlashes>()
            .ActivateOnEnter<AlabasterRaidwides>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(AlabasterGuardianStates),
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Contributors = "KanoNoUta",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CriticalEngagement,
    GroupID = 1093u,
    NameID = 51u,
    SortOrder = 3)]
public sealed class AlabasterGuardian(WorldState ws, Actor primary) : BossModule(ws, primary, new(-519f, -641f), new ArenaBoundsCircle(20f));
