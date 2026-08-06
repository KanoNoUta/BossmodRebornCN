namespace BossMod.Autorotation;

[ConfigDisplay(Name = "自动循环（非 Combat Reborn 支持）", Order = 5)]
public sealed class AutorotationConfig : ConfigNode
{
    [PropertyDisplay("显示游戏内界面")]
    public bool ShowUI = false;

    public enum DtrStatus
    {
        [PropertyDisplay("禁用")]
        None,
        [PropertyDisplay("仅文字")]
        TextOnly,
        [PropertyDisplay("图标")]
        Icon
    }

    [PropertyDisplay("在服务器信息栏显示自动循环预设")]
    public DtrStatus ShowDTR = DtrStatus.None;

    [PropertyDisplay("隐藏 VBM 默认预设", tooltip: "如果你已创建了自己的预设且不再需要内置默认预设，此选项将阻止其在自动循环和预设编辑器窗口中显示。")]
    public bool HideDefaultPresets = true;

    public bool SuggestHealerAI = true;

    [PropertyDisplay("显示身位提示", tooltip: "显示身位技能提示，指示移动到目标侧面或背面")]
    public bool ShowPositionals = false;

    [PropertyDisplay("Follow RotationSolverReborn's desired positional", tooltip: "When enabled, the 'Misc AI: Goes to specified positional' rotation module will override its Positional track setting and instead use the positional currently requested by RotationSolverReborn over IPC (Does not apply to Target Dummies)")]
    public bool FollowRSRDesiredPositional = true;

    [PropertyDisplay("死亡时自动关闭自动循环")]
    public bool ClearPresetOnDeath = true;

    [PropertyDisplay("脱战时自动关闭自动循环")]
    public bool ClearPresetOnCombatEnd = false;

    [PropertyDisplay("触发诱饵陷阱时自动关闭自动循环", tooltip: "仅适用于深层迷宫")]
    public bool ClearPresetOnLuring = false;

    [PropertyDisplay("脱战时自动重新启用强制关闭的自动循环")]
    public bool ClearForceDisableOnCombatEnd = true;

    [PropertyDisplay("提前开怪阈值", tooltip: "倒计时大于此值时有人进入战斗，视为抢开，自动循环将被强制关闭")]
    [PropertySlider(0, 30, Speed = 1)]
    public float EarlyPullThreshold = 1.5f;

    [PropertyDisplay("Disable autorotation if the boss is pulled without a countdown", tooltip: "Only applies if you have a cooldown plan active.")]
    public bool PlannedPullSafety = true;
}
