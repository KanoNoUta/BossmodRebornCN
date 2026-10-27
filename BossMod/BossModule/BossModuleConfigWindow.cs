using Dalamud.Bindings.ImGui;

namespace BossMod;

public sealed class BossModuleConfigWindow : UIWindow
{
    private readonly BossModuleRegistry.Info _info;
    private readonly ConfigNode? _node;
    private readonly PartyRolesConfig _prc = Service.Config.Get<PartyRolesConfig>();
    private readonly WorldState _ws;
    private readonly UITree _tree = new();
    private readonly UITabs _tabs = new("ConfigTabs");

    public BossModuleConfigWindow(BossModuleRegistry.Info info, WorldState ws) : base($"{info.ModuleType.Name} config", true, new(1200, 800))
    {
        _info = info;
        _node = info.ConfigType != null ? Service.Config.Get<ConfigNode>(info.ConfigType) : null;
        _ws = ws;
        _tabs.Add("副本专用设置", DrawEncounterTab);
        _tabs.Add("队伍职能分配", DrawPartyRolesAssignmentsTab);
    }

    public override void Draw() => _tabs.Draw();

    private void DrawEncounterTab()
    {
        if (_info.HasPrePullHints)
        {
            ConfigUI.DrawPrePullHintSetting(_info);
            if (_node != null)
                ImGui.Separator();
        }

        if (_node != null)
            ConfigUI.DrawNode(_node, Service.Config, _tree, _ws);
        else if (!_info.HasPrePullHints)
            ImGui.TextUnformatted("此模块没有可配置的选项");
    }

    private void DrawPartyRolesAssignmentsTab()
    {
        if (_ws.Party.Player() != null)
        {
            ConfigUI.DrawNode(_prc, Service.Config, _tree, _ws);
        }
    }
}
