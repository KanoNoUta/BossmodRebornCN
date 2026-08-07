using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN3Necrophobia;

// Normal 魔之塔 Boss3: Necrophobia. 爆炎 18m 圈、冰封十字 45x15、古代暴雷 60y 45 度扇、
// 灭亡射线 30x6 直条、黑暗奔流 60x10 直条 + 左右步进地火、真空波 180 度。
sealed class NecrophobiaAOEs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCircle Fire = new(18f);
    private static readonly AOEShapeCross Blizzard = new(45f, 7.5f);
    private static readonly AOEShapeCone Thunder = new(60f, 22.5f.Degrees());
    private static readonly AOEShapeRect DeathlyRay = new(30f, 3f);
    private static readonly AOEShapeRect DarkCurrent = new(60f, 5f);
    private static readonly AOEShapeRect DarkCurrentTread = new(10f, 30f);
    private static readonly AOEShapeCone Vacuum = new(30f, 90f.Degrees());

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.SeveredFireIII or (uint)AID.AncientFireIII1 => new(Fire),
        (uint)AID.SeveredBlizzardIII or (uint)AID.AncientBlizzardIII1 => new(Blizzard),
        (uint)AID.AncientThunderIII1 or (uint)AID.AncientThunderIII3 => new(Thunder),
        (uint)AID.DeathlyRay => new(DeathlyRay),
        (uint)AID.DarkCurrent1 => new(DarkCurrent),
        (uint)AID.DarkCurrent2 => new(DarkCurrentTread),
        (uint)AID.VacuumWave => new(Vacuum),
        _ => null
    };
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    Contributors = "KanoNoUta",
    PrimaryActorOID = (uint)OID.Necrophobia,
    GroupType = BossModuleInfo.GroupType.TheForkedTowerMagic,
    GroupID = 1017u,
    NameID = 0u,
    SortOrder = 3,
    Category = BossModuleInfo.Category.Foray,
    Expansion = BossModuleInfo.Expansion.Dawntrail)]
public sealed class Necrophobia : BossModule
{
    public Necrophobia(WorldState ws, Actor primary) : base(ws, primary, new(100f, 789f), new ArenaBoundsSquare(25f))
        => Service.Logger.Information($"[FT] {GetType().Name} created (oid={primary.OID:X})");

    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actor(PrimaryActor, allowDeadAndUntargetable: true);
}
