using Dalamud.Bindings.ImGui;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BossMod;

[ConfigDisplay(Name = "首领模块与雷达", Order = 1)]
public sealed class BossModuleConfig : ConfigNode
{
    public bool RadarResize;

    public override void DrawCustom(UITree tree, WorldState ws)
    {
        if (ImGui.Button("窗口重新居中"))
        {
            Service.BossModWindow?.RecenterWindow();
        }
    }

    // boss module settings
    [PropertyDisplay("加载模块所需的最低完成度", tooltip: "部分模块处于“开发中”状态，除非修改此设置，否则不会自动加载。")]
    public BossModuleInfo.Maturity MinMaturity = BossModuleInfo.Maturity.Contributed;

    // Modules explicitly disabled from Supported fights. Primary actor OIDs are used as unique module IDs.
    public uint[] DisabledModuleOIDs = [];
    [JsonIgnore]
    internal HashSet<uint>? _disabledModuleOIDs;

    [PropertyDisplay("允许模块自动使用技能", tooltip: "例如：模块可以在击退发生前自动使用防击退技能。")]
    public bool AllowAutomaticActions = true;

    [PropertyDisplay("显示测试雷达和提示窗口", tooltip: "无需进入首领战即可调整雷达和提示窗口。", separator: true, depends: nameof(EnableRadar))]
    public bool ShowDemo = false;

    // radar window settings
    [PropertyDisplay("启用雷达", separator: true)]
    public bool EnableRadar = true;

    [PropertyDisplay("将雷达投影到 3D 场景中")]
    public bool ProjectRadarInto3DWorld = false;

    [PropertyDisplay("在 3D 场景中显示角色三角标记", tooltip: "显示普通角色的三角标记。关闭后机制标记（包括击退落点）仍会显示。", depends: nameof(ProjectRadarInto3DWorld))]
    public bool ShowActorTrianglesIn3DWorld = true;

    [PropertyDisplay("在 3D 场景中绘制场地边界", tooltip: "开启雷达 3D 投影时，也可以绘制场地边界。", depends: nameof(ProjectRadarInto3DWorld))]
    public bool EnableArenaOutlineIn3DWorld = true;

    [PropertyDisplay("在 3D 场景中绘制文字和图标标牌", tooltip: "开启雷达 3D 投影时，也可以绘制文字和图标标牌。", depends: nameof(ProjectRadarInto3DWorld))]
    public bool EnableTextIconBillboards = true;

    [PropertyDisplay("标牌离地高度", tooltip: "标牌显示在地面上方多少亚姆，包括背对、文字和图标。", depends: nameof(ProjectRadarInto3DWorld))]
    [PropertySlider(0f, 20f, Speed = 0.1f, Logarithmic = true)]
    public float BillboardHeightOffset = 5f;

    [PropertyDisplay("文字标牌字号", tooltip: "调整 3D 场景中文字标牌的大小。", depends: nameof(ProjectRadarInto3DWorld))]
    [PropertySlider(17f, 250f, Speed = 0.5f, Logarithmic = true)]
    public float TextBillboardFontSize = 110f;

    [PropertyDisplay("图标标牌字号", tooltip: "调整 3D 场景中图标标牌的大小。", separator: true, depends: nameof(ProjectRadarInto3DWorld))]
    [PropertySlider(17f, 250f, Speed = 0.5f, Logarithmic = true)]
    public float IconBillboardFontSize = 110f;

    [PropertyDisplay("锁定雷达和提示窗口的位置及鼠标交互")]
    public bool Lock = false;

    [PropertyDisplay("雷达窗口背景透明", tooltip: "移除雷达周围的黑色窗口背景；如果将雷达移到其他显示器，此功能可能失效。")]
    public bool TrishaMode = true;

    [PropertyDisplay("为雷达场地添加不透明背景")]
    public bool OpaqueArenaBackground = true;

    [PropertyDisplay("显示雷达标记的轮廓和阴影")]
    public bool ShowOutlinesAndShadows = true;

    [PropertyDisplay("雷达场地缩放比例", tooltip: "雷达窗口内场地的缩放比例。")]
    [PropertySlider(0.1f, 10f, Speed = 0.1f, Logarithmic = true)]
    public float ArenaScale = 1f;

    [PropertyDisplay("雷达元素线条粗细比例", tooltip: "统一缩放雷达元素的轮廓粗细。")]
    [PropertySlider(0.1f, 10f, Speed = 0.1f, Logarithmic = true)]
    public float ThicknessScale = 1f;

    [PropertyDisplay("旋转雷达以匹配镜头方向")]
    public bool RotateArena = true;

    [PropertyDisplay("关闭地图旋转时将地图旋转 180°")]
    public bool FlipArena = false;

    [PropertyDisplay("为雷达旋转预留额外空间", tooltip: "使用上方设置时，可在雷达边缘裁切前预留额外空间，避免战斗中旋转镜头或显示方位文字时内容被裁掉。")]
    [PropertySlider(1f, 2f, Speed = 0.1f, Logarithmic = true)]
    public float SlackForRotations = 1.5f;

    [PropertyDisplay("在雷达中显示场地边界")]
    public bool ShowBorder = true;

    [PropertyDisplay("玩家处于危险时改变场地边界颜色", tooltip: "当玩家站在可能被机制命中的位置时，将白色边界改为红色。")]
    public bool ShowBorderRisk = true;

    [PropertyDisplay("玩家处于危险时屏幕边缘闪烁", tooltip: "玩家有警告时，屏幕边缘以危险场地边界颜色（敌人颜色）脉动发光。不依赖雷达和 3D 投影设置。")]
    public bool ShowScreenRiskBorder = false;

    [PropertyDisplay("屏幕危险闪烁强度", depends: nameof(ShowScreenRiskBorder))]
    [PropertySlider(0f, 10f, Speed = 0.1f)]
    public float ScreenRiskBorderIntensity = 2.5f;

    [PropertyDisplay("在雷达中显示方位名称")]
    public bool ShowCardinals = false;

    [PropertyDisplay("方位名称字号", depends: nameof(ShowCardinals))]
    [PropertySlider(0.1f, 100f, Speed = 1f)]
    public float CardinalsFontSize = 17f;

    [PropertyDisplay("在雷达中显示场地标点")]
    public bool ShowWaymarks = false;

    [PropertyDisplay("场地标点字号", depends: nameof(ShowWaymarks))]
    [PropertySlider(0.1f, 100f, Speed = 1f)]
    public float WaymarkFontSize = 22f;

    [PropertyDisplay("在雷达中显示目标标记（攻击、止步、禁止及形状标记）")]
    public bool ShowSigns = false;

    [PropertyDisplay("始终显示所有存活的队员")]
    public bool ShowIrrelevantPlayers = false;

    [PropertyDisplay("按职能为雷达中未着色的玩家上色")]
    public bool ColorPlayersBasedOnRole = false;

    [PropertyDisplay("始终显示被设为焦点目标的队员", separator: true)]
    public bool ShowFocusTargetPlayer = false;

    [PropertyDisplay("Actor triangle scale factor")]
    [PropertySlider(0.1f, 10f, Speed = 0.1f)]
    public float ActorScale = 1f;

    // hint window settings
    [PropertyDisplay("开战前显示副本提示弹窗", tooltip: "开怪前显示该战斗的专属说明。可在弹窗中对单场战斗永久隐藏，并在该战斗的配置窗口中重新启用。")]
    public bool ShowPrePullHints = true;

    // Persisted separately from module-specific config so every encounter can support "Never show again". Primary actor OIDs are used as unique module IDs.
    public uint[] SuppressedPrePullHintOIDs = [];

    [JsonIgnore]
    internal HashSet<uint>? _suppressedPrePullHintOIDs;

    [PropertyDisplay("在独立窗口中显示文字提示", tooltip: "将提示窗口与雷达窗口分离，以便单独调整提示窗口的位置。")]
    public bool HintsInSeparateWindow = false;

    [PropertyDisplay("使独立提示窗口透明")]
    public bool HintsInSeparateWindowTransparent = false;

    [PropertyDisplay("显示机制顺序和计时提示")]
    public bool ShowMechanicTimers = true;

    [PropertyDisplay("显示全队提示")]
    public bool ShowGlobalHints = true;

    [PropertyDisplay("显示玩家提示和警告", separator: true)]
    public bool ShowPlayerHints = true;

    // misc. settings
    [PropertyDisplay("在游戏世界中显示移动提示", tooltip: "部分机制会在游戏世界中显示箭头，指示应移动到的位置。")]
    public bool ShowWorldArrows = false;

    [PropertyDisplay("显示近战范围指示器")]
    public bool ShowMeleeRangeIndicator = false;

    [PropertyDisplay("最大加载距离", tooltip: "最大加载距离，单位为亚姆（出于安全最低为 100 亚姆）。首领超过此距离时不会加载模块，已加载的模块也不会卸载。")]
    [PropertySlider(100f, 500f, Speed = 0.1f, Logarithmic = true)]
    public float MaxLoadDistance = 500f;

    public override void Deserialize(JsonElement j, JsonSerializerOptions ser)
    {
        base.Deserialize(j, ser);
        _disabledModuleOIDs = null;
        _suppressedPrePullHintOIDs = null;
    }

    public bool IsModuleEnabled(uint primaryActorOID) => !DisabledModuleOIDSet().Contains(primaryActorOID);

    public bool IncludeInSupportedFightControls(BossModuleRegistry.Info info)
        => info.Maturity != BossModuleInfo.Maturity.Dummy || MinMaturity == BossModuleInfo.Maturity.Dummy;

    public void SetModuleEnabled(uint primaryActorOID, bool enabled)
    {
        var set = DisabledModuleOIDSet();
        var disabled = set.Contains(primaryActorOID);
        if (enabled == !disabled)
        {
            return;
        }

        if (enabled)
        {
            set.Remove(primaryActorOID);
        }
        else
        {
            set.Add(primaryActorOID);
        }

        PersistDisabledModuleOIDs(set);
        Modified.Fire();
    }

    public void SetModulesEnabled(List<uint> primaryActorOIDs, bool enabled)
    {
        var set = DisabledModuleOIDSet();
        var changed = false;
        var count = primaryActorOIDs.Count;
        for (var i = 0; i < count; ++i)
        {
            changed |= enabled ? set.Remove(primaryActorOIDs[i]) : set.Add(primaryActorOIDs[i]);
        }

        if (changed)
        {
            PersistDisabledModuleOIDs(set);
            Modified.Fire();
        }
    }

    public (bool anyEnabled, bool allEnabled) ModulesEnabledState(List<uint> primaryActorOIDs)
    {
        var set = DisabledModuleOIDSet();
        var anyEnabled = false;
        var allEnabled = true;
        var count = primaryActorOIDs.Count;
        for (var i = 0; i < count; ++i)
        {
            var enabled = !set.Contains(primaryActorOIDs[i]);
            anyEnabled |= enabled;
            allEnabled &= enabled;
        }
        return (anyEnabled, count > 0 && allEnabled);
    }

    public void SetExpansionEnabled(BossModuleInfo.Expansion expansion, bool enabled)
    {
        var set = DisabledModuleOIDSet();
        var changed = false;
        foreach (var info in BossModuleRegistry.RegisteredModules.Values)
        {
            if (info.Expansion != expansion || !IncludeInSupportedFightControls(info))
            {
                continue;
            }
            changed |= enabled ? set.Remove(info.PrimaryActorOID) : set.Add(info.PrimaryActorOID);
        }

        if (changed)
        {
            PersistDisabledModuleOIDs(set);
            Modified.Fire();
        }
    }

    public void SetCategoryEnabled(BossModuleInfo.Category category, bool enabled)
    {
        var set = DisabledModuleOIDSet();
        var changed = false;
        foreach (var info in BossModuleRegistry.RegisteredModules.Values)
        {
            if (info.Category != category || !IncludeInSupportedFightControls(info))
            {
                continue;
            }
            changed |= enabled ? set.Remove(info.PrimaryActorOID) : set.Add(info.PrimaryActorOID);
        }

        if (changed)
        {
            PersistDisabledModuleOIDs(set);
            Modified.Fire();
        }
    }

    public (bool anyEnabled, bool allEnabled) ExpansionEnabledState(BossModuleInfo.Expansion expansion)
    {
        var set = DisabledModuleOIDSet();
        var anyEnabled = false;
        var allEnabled = true;
        var any = false;
        foreach (var info in BossModuleRegistry.RegisteredModules.Values)
        {
            if (info.Expansion != expansion || !IncludeInSupportedFightControls(info))
            {
                continue;
            }
            any = true;
            var enabled = !set.Contains(info.PrimaryActorOID);
            anyEnabled |= enabled;
            allEnabled &= enabled;
        }
        return (anyEnabled, any && allEnabled);
    }

    public (bool anyEnabled, bool allEnabled) CategoryEnabledState(BossModuleInfo.Category category)
    {
        var set = DisabledModuleOIDSet();
        var anyEnabled = false;
        var allEnabled = true;
        var any = false;
        foreach (var info in BossModuleRegistry.RegisteredModules.Values)
        {
            if (info.Category != category || !IncludeInSupportedFightControls(info))
            {
                continue;
            }
            any = true;
            var enabled = !set.Contains(info.PrimaryActorOID);
            anyEnabled |= enabled;
            allEnabled &= enabled;
        }
        return (anyEnabled, any && allEnabled);
    }

    private HashSet<uint> DisabledModuleOIDSet()
    {
        if (_disabledModuleOIDs != null)
        {
            return _disabledModuleOIDs;
        }

        var len = DisabledModuleOIDs.Length;
        var set = new HashSet<uint>(len);
        for (var i = 0; i < len; ++i)
        {
            set.Add(DisabledModuleOIDs[i]);
        }
        return _disabledModuleOIDs = set;
    }

    private void PersistDisabledModuleOIDs(HashSet<uint> set)
    {
        var persisted = new uint[set.Count];
        set.CopyTo(persisted);
        Array.Sort(persisted);
        DisabledModuleOIDs = persisted;
    }

    public bool ShowPrePullHintsFor(uint primaryActorOID) => !SuppressedPrePullHintOIDSet().Contains(primaryActorOID);

    public void SetShowPrePullHintsFor(uint primaryActorOID, bool show)
    {
        var set = SuppressedPrePullHintOIDSet();
        var suppressed = set.Contains(primaryActorOID);
        if (show == !suppressed)
        {
            return;
        }

        if (show)
        {
            set.Remove(primaryActorOID);
        }
        else
        {
            set.Add(primaryActorOID);
        }

        var persisted = new uint[set.Count];
        set.CopyTo(persisted);
        Array.Sort(persisted);
        SuppressedPrePullHintOIDs = persisted;
        Modified.Fire();
    }

    private HashSet<uint> SuppressedPrePullHintOIDSet()
    {
        if (_suppressedPrePullHintOIDs != null)
        {
            return _suppressedPrePullHintOIDs;
        }

        var len = SuppressedPrePullHintOIDs.Length;
        var set = new HashSet<uint>(len);
        for (var i = 0; i < len; ++i)
        {
            set.Add(SuppressedPrePullHintOIDs[i]);
        }
        return _suppressedPrePullHintOIDs = set;
    }
}
