using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace PluginChatOverlay;

public sealed partial class Plugin
{
    private int settingsPage, requestedSettingsPage = -1;
    private bool settingsChanged, resetSettingsScroll;
    private bool? lastSettingsMinasis;
    private Action? settingsSidebar, settingsDetail;
    private static readonly (string Id, string Label, FontAwesomeIcon Icon)[] SettingsPages =
    [
        ("basic", "基本", FontAwesomeIcon.Cog),
        ("appearance", "表示・色", FontAwesomeIcon.PaintBrush),
        ("interaction", "操作", FontAwesomeIcon.MousePointer),
        ("tabs", "チャットタブ", FontAwesomeIcon.Comments),
        ("history", "履歴", FontAwesomeIcon.History),
        ("diagnostics", "診断・共存", FontAwesomeIcon.InfoCircle),
    ];

    private void DrawConfig()
    {
        if (resetConfigSize) { ImGui.SetNextWindowSize(new(740, 540), ImGuiCond.Always); resetConfigSize = false; }
        if (minasisFrame) ui.ConstrainNextTwoColumnWindow();
        if (focusConfig)
        {
            ImGui.SetNextWindowFocus(); ImGui.SetNextWindowCollapsed(false, ImGuiCond.Always);
            focusConfig = false;
        }
        var draw = ImGui.Begin("Plugin Chat Overlay 設定###PluginChatOverlay.Config", ref configOpen, ImGuiWindowFlags.NoSavedSettings);
        try
        {
            if (!draw) return;
            if (lastSettingsMinasis != minasisFrame)
            {
                requestedSettingsPage = settingsPage;
                resetSettingsScroll = true;
                lastSettingsMinasis = minasisFrame;
            }
            if (selectChatTabSettings)
            {
                settingsPage = requestedSettingsPage = 3;
                resetSettingsScroll = true; selectChatTabSettings = false;
            }
            settingsChanged = false;
            if (minasisFrame)
            {
                settingsSidebar ??= DrawSettingsSidebar;
                settingsDetail ??= DrawSettingsPage;
                ui.TwoColumn("SettingsLayout", settingsSidebar, settingsDetail);
            }
            else
            {
                var body = ImGui.BeginChild("SettingsContent", Vector2.Zero, false);
                try
                {
                    if (body && ImGui.BeginTabBar("SettingsTabs"))
                    {
                        try
                        {
                            for (var i = 0; i < SettingsPages.Length; i++)
                            {
                                if (!ImGui.BeginTabItem(SettingsPages[i].Label, requestedSettingsPage == i ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None)) continue;
                                try
                                {
                                    if (settingsPage != i) resetSettingsScroll = true;
                                    settingsPage = i;
                                    if (requestedSettingsPage == i) requestedSettingsPage = -1;
                                    DrawSettingsPage();
                                }
                                finally { ImGui.EndTabItem(); }
                            }
                        }
                        finally { ImGui.EndTabBar(); }
                    }
                }
                finally { ImGui.EndChild(); }
            }
            if (settingsChanged) { presentationRevision++; ApplyUiVisibility(); Save(); }
        }
        finally { ImGui.End(); }
    }
    private void DrawSettingsSidebar()
    {
        ui.Heading("Plugin Chat Overlay");
        ui.Muted("Ver " + typeof(Plugin).Assembly.GetName().Version?.ToString(3));
        ui.Separator();
        for (var i = 0; i < SettingsPages.Length; i++)
        {
            var page = SettingsPages[i];
            if (!ui.NavigationItem(page.Id, page.Label, settingsPage == i, page.Icon.ToIconString(), UiBuilder.IconFont)) continue;
            if (settingsPage != i) resetSettingsScroll = true;
            settingsPage = i; requestedSettingsPage = -1;
        }
        ui.Separator();
        settingsChanged |= DrawUiStyleSettings(sidebar: true);
    }
    private void DrawSettingsPage()
    {
        if (resetSettingsScroll) { ImGui.SetScrollY(0); resetSettingsScroll = false; }
        settingsChanged |= settingsPage switch
        {
            0 => DrawBasicSettings(),
            1 => DrawAppearance(),
            2 => DrawInteraction(),
            3 => DrawTabSettings(),
            4 => DrawHistorySettings(),
            _ => DrawSettingsDiagnostics(),
        };
        if (saveNotice.Length > 0) ImGui.TextWrapped(saveNotice);
    }
    private bool DrawSettingsDiagnostics() { DrawDiagnostics(); return false; }
}
