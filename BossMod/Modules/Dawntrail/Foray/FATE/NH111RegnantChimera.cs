namespace BossMod.Dawntrail.Foray.FATE.NH111RegnantChimera;

public enum OID : uint
{
    RegnantChimera = 0x4C7D, // R5.180
    FulmipotentOrb = 0x4C7F,
    ChaoticNoise = 0x4B71,
}

public enum AID : uint
{
    AutoAttack = 50856, // RegnantChimera->player, no cast, single-target
    DragonsVoice = 48636, // FulmipotentOrb->self, 4.0s cast, range 8-30 donut
    LeftDuobreath = 50111, // RegnantChimera->self, 5.0s cast, range 40 180-degree cone
    RightDuobreath = 50112, // RegnantChimera->self, 5.0s cast, range 40 180-degree cone
    Cacophony = 50113, // RegnantChimera->self, 4.0s cast, single-target
    ChaoticChorus = 50114, // ChaoticNoise->self, 1.5s cast, range 6 circle
    DragonsBreath = 50115, // RegnantChimera->self, no cast, range 40 180-degree cone
    RamsBreath = 50116, // RegnantChimera->self, no cast, range 40 180-degree cone
}

sealed class Duobreath(BossModule module) : Components.SimpleAOEGroups(module,
    [(uint)AID.LeftDuobreath, (uint)AID.RightDuobreath], new AOEShapeCone(40f, 90f.Degrees()));
sealed class ChaoticChorus(BossModule module) : Components.SimpleAOEs(module, (uint)AID.ChaoticChorus, new AOEShapeCircle(6f));
sealed class DragonsVoice(BossModule module) : Components.SimpleAOEs(module, (uint)AID.DragonsVoice, new AOEShapeDonut(8f, 30f));

[SkipLocalsInit]
sealed class RegnantChimeraStates : StateMachineBuilder
{
    public RegnantChimeraStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<Duobreath>()
            .ActivateOnEnter<ChaoticChorus>()
            .ActivateOnEnter<DragonsVoice>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(RegnantChimeraStates),
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.RegnantChimera,
    Contributors = "KanoNoUta",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.ForayFATE,
    GroupID = 1093u,
    NameID = 2076u,
    SortOrder = 1)]
[SkipLocalsInit]
public sealed class RegnantChimera(WorldState ws, Actor primary) : OpenWorldFate(ws, primary);
