<div align="center">

<a href="https://github.com/KanoNoUta/BossmodRebornCN"><img src="https://raw.githubusercontent.com/FFXIV-CombatReborn/RebornAssets/main/IconAssets/BMR_Icon.png" width="128" alt="BossMod Reborn CN"></a>

# BossMod Reborn CN

**面向国服的 BossMod Reborn 适配版**

![Github Latest Releases](https://img.shields.io/github/downloads/KanoNoUta/BossmodRebornCN/latest/total.svg?style=for-the-badge)
![Github License](https://img.shields.io/github/license/KanoNoUta/BossmodRebornCN.svg?label=License&style=for-the-badge)

</div>

BossMod Reborn CN 由 KanoNoUta 维护，基于 [FFXIV-CombatReborn/BossmodReborn](https://github.com/FFXIV-CombatReborn/BossmodReborn) 定期同步上游，当前对应国服 7.55。插件提供战斗雷达、机制范围、移动提示与职业辅助，界面已全部汉化。

- 全部功能直接可用，无需激活码、CID 授权或验证服务。
- Release 使用原始编译输出，不做混淆。

## 安装

在 Dalamud 设置的「测试版」→「自定义插件仓库」中添加：

```
https://raw.githubusercontent.com/KanoNoUta/DalamudPlugins/main/pluginmaster.json
```

保存后在插件列表中搜索 **BossModReborn 斗兽版本** 安装。BossMod 斗兽版本与原版 BossMod / BossModReborn 共用战斗接口，不能同时启用。

## 功能

| 功能 | 说明 |
| --- | --- |
| 战斗雷达 | 屏幕小地图显示玩家、首领位置和即将到来的范围技能，可投影到 3D 场景中 |
| 机制提示 | 雷达旁显示即将到来的机制、全队处理提示和针对自己的建议 |
| 冷却规划 | 按战斗时间轴规划技能和减伤的使用时机 |
| AI 跟随 | 自动躲避范围、跟随队友，可分别设置近战/坦克与远程/治疗的目标距离 |
| 模块浏览器 | 按资料片和内容类型筛选、启用或禁用各场战斗的模块 |

## 国服专属适配

### 斗兽自走棋

- 支持普通一、二、三盘及高一、高二的现有录像覆盖线路，运行时按名字映射国服真实 OID，首次进入即可加载。
- 机制转火只自动选一次目标，之后尊重手动切换；这套逻辑只在斗兽模块中生效。
- 劳妲未被录像覆盖的后续变式仍需实战确认。

### 新月岛北岛

- 已适配现有录像覆盖的 CE50–CE63（CE49 暂无录像，按当前范围暂缓）。
- 古术魔典、卡洛菲斯提莉二重身、赤龙、新月阿剌克涅等完整机制模块。
- 北岛 FATE 绘制，包括遗物冰狼、统领奇美拉等。
- 处理高倍速回放重复包、读条重同步、迟到事件与残留范围清理。

### 魔之塔

- 普魔之塔四个首领按国服回放适配场地与机制。
- 超魔之塔暂未在国服版本中注册。

## 更新日志

每个版本的改动见插件内的更新日志，或插件库中的 [BossModReborn.json](https://github.com/KanoNoUta/DalamudPlugins/blob/main/plugins/BossModReborn/BossModReborn.json)。

## 反馈与贡献

- 遇到问题请附上副本名、首领名和现象，有 BMR 回放（`/bmr` → 回放）会更快定位。
- 贡献代码：Fork 本仓库，修改并测试后向 `main` 提交 PR。通用机制修复建议直接提交给[上游](https://github.com/FFXIV-CombatReborn/BossmodReborn)。

## 致谢

本项目基于 [BossmodReborn](https://github.com/FFXIV-CombatReborn/BossmodReborn) 及其前身 [BossMod](https://github.com/awgil/ffxiv_bossmod)。第三方组件的许可见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)，本项目许可见 [LICENSE](LICENSE)。
