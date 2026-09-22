namespace BossMod.Dawntrail.Foray.Crucible;

// 第 29 组（斗兽奇弈高段 第一盘 2026-09-11 21.10.10 实测 OID）：
//   奇子·树精[19661] 本体、奇子·蛞蝓[19662]×12、奇子·花苗(树苗)[19663]、奇子·凶蛛蝎[19664]、奇子·比洛克[19665]。
// 技能范围由 CrucibleTreantAOEs 处理；目标由玩家手选，只保留远程站位偏好。
sealed class CrucibleGroveAdds(BossModule module) : BossComponent(module)
{
    private static readonly uint[] AddOIDs = [19661u, 19662u, 19663u, 19664u, 19665u];

    private IEnumerable<Actor> Living() => Module.Enemies(AddOIDs)
        .Where(a => !a.IsDeadOrDestroyed && a.IsTargetable);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // No automatic add selection, including first spawn or later waves.
        // Also stop AutoTarget Always from replacing the player's selection.
        hints.PreserveTarget = true;
        foreach (var add in Living())
        {
            if (hints.FindEnemy(add) is { } enemy)
                enemy.StayAtLongRange = true;
        }
    }
}
