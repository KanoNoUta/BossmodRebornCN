# [![](https://raw.githubusercontent.com/FFXIV-CombatReborn/RebornAssets/main/IconAssets/BMR_Icon.png)](https://github.com/KanoNoUta/BossmodRebornCN)

**BossMod Reborn CN**

![Github Latest Releases](https://img.shields.io/github/downloads/KanoNoUta/BossmodRebornCN/latest/total.svg?style=for-the-badge)
![Github License](https://img.shields.io/github/license/KanoNoUta/BossmodRebornCN.svg?label=License&style=for-the-badge)

BossMod Reborn CN 是由 KanoNoUta 维护的国服适配版本，当前对应国服 7.55。插件提供战斗雷达、机制范围、移动提示与职业辅助。

当前 7.5.6.11 版本支持斗兽自走棋部分线路，全部功能可直接使用，无需激活码、CID 授权或验证服务。Release 使用原始编译输出，不执行混淆。劳妲未被录像覆盖的后续变式仍需实战确认。

测试通道 7.5.6.12：优化斯芬克斯数字格处理与答题状态清理；修复劳妲击退站位、人物面向及后续半场落点，叠加圆形范围时分阶段就位；Boss和小怪自动选择一次后放开手动目标。 两次劳妲击退已通过录像驱动的80/300ms延迟导航回归，需重载后实战复核。

7.5.6.11：补充斗兽普通一、二、三盘及高一机制与场地轮廓；修正连续击退、落点及范围预警；爆弹小怪不再持续躲跑；修复与 AutoDuty 同开时误选自身的问题。保留石像鬼特化拉线。

7.5.6.10 将高一尾王四连落毒调整为 14×7y 矩形：②③靠场边，①④靠场心；②③毒圈边缘保留 2y 通道，①②和③④间距保持 7y。

## 国服 7.55 新月岛北岛

- 已适配现有录像覆盖的 CE50–CE63（CE49 提蔛暂无录像，按当前范围暂缓）。
- 新增古术魔典、卡洛菲斯提莉二重身、赤龙与新月阿剌克涅的完整机制模块。
- 已处理高倍速回放重复包、CastInfo 重同步、迟到事件与残留范围清理。
- 惨白魔人的死亡轮盘按实测 5–12m/12–20m 极坐标扇区绘制，并跟随 helper 的实时坐标、朝向与极性换位。
- 阿尔戈尔旋转拉拽、连续半场、圆形击退与横向击退均按 replay/客户端 ActionEffect 的实际时序处理。
- 7.5.5.8 修正雪石膏之剑与宝石兽半场方向、卡洛菲斯提莉左右刀、魔亡灵法师直条和古术魔典圆形场地，并补强阿尔戈尔旋转吸引 AI 与诱拐魔冰花提示。
- 7.5.5.12 开启北岛四 FATE 绘制，补齐惨白魔人电网与轮盘预览，修正阿尔戈尔地火/场地、负隅宝石兽与魔亡灵法师方形电网、连续击退、变形法师冲刺与扩散圈，重做禁书知见规则/翻页/墨阵/踩塔。
- 补全统领奇美拉三连吐息、玛琦塔八连挥击与唤雷者 Freefall 三段落点。

第三方插件库：`https://raw.githubusercontent.com/KanoNoUta/DalamudPlugins/main/pluginmaster.json`

## Features

- **Advanced Radar System**: A sophisticated on-screen map displaying player and boss positions, imminent AOEs, and other crucial mechanics. This system helps players visualize the battlefield, simplifying decision-making processes.
- **Mechanic Descriptors**: Near the radar, you’ll find clear, concise descriptions of upcoming mechanics, global hints for resolving current challenges, and personalized player advice to optimize your response to each situation.
- **Cooldown Planner**: A tool for meticulous planning of ability usage, ensuring optimal timing for cooldowns and abilities in coordination with raid strategies.
- **User-Friendly Interface**: The module viewer and configuration interface are designed for quick access during combat preparation.
- **Regular Updates**: Committed to staying current with the latest game patches, class updates, and community feedback. PRs will be reviewed, tested, and approved.

## Contributing

- Create a fork
- Make your changes
- Test the changes
- Create a PR and point it to main
