using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace PluginChatOverlay;

public sealed partial class Plugin
{
    private int settingsPage, requestedSettingsPage = -1;
    private bool settingsChanged, resetSettingsScroll;
    private bool? lastSettingsMinasis;

    private static readonly string[] SettingsPages = ["基本", "タブ", "レイアウト", "操作", "履歴", "診断・共存"];

    private void DrawConfig()
    {
        if (minasisFrame) { DrawWorkspaceConfig(); return; }
        if (resetConfigSize) { ImGui.SetNextWindowSize(new(settings.ConfigWidth, settings.ConfigHeight), ImGuiCond.Always); resetConfigSize = false; }

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
                settingsPage = requestedSettingsPage = 1;
                resetSettingsScroll = true; selectChatTabSettings = false;
            }
            settingsChanged = UpdateConfigWindowSize();
            var renderSettingsBefore = RenderSettingsState.Capture(settings);
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
                                if (!ImGui.BeginTabItem(SettingsPages[i], requestedSettingsPage == i ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None)) continue;
                                try
                                {
                                    if (settingsPage != i) resetSettingsScroll = true;
                                    settingsPage = i;
                                    if (requestedSettingsPage == i) requestedSettingsPage = -1;
                                    var pageBody = ImGui.BeginChild("SettingsPage-" + i, Vector2.Zero, false);
                                    try { if (pageBody) DrawSettingsPage(); }
                                    finally { ImGui.EndChild(); }
                                }
                                finally { ImGui.EndTabItem(); }
                            }
                        }
                        finally { ImGui.EndTabBar(); }
                    }
                }
                finally { ImGui.EndChild(); }
            }
            if (settingsChanged) { UpdateRenderSettings(renderSettingsBefore); ApplyUiVisibility(); Save(); }
        }
        finally { ImGui.End(); }
    }
    private bool UpdateConfigWindowSize()
    {
        // Persist once resizing ends; content tabs and layout resets preserve this size.
        if (ImGui.IsMouseDown(ImGuiMouseButton.Left)) return false;
        var size = ImGui.GetWindowSize();
        if (Math.Abs(size.X - settings.ConfigWidth) < 1 && Math.Abs(size.Y - settings.ConfigHeight) < 1) return false;
        settings.ConfigWidth = size.X; settings.ConfigHeight = size.Y;
        return true;
    }
    private void DrawSettingsPage()
    {
        if (resetSettingsScroll) { ImGui.SetScrollY(0); resetSettingsScroll = false; }
        settingsChanged |= settingsPage switch
        {
            0 => DrawBasicSettings(),
            1 => DrawTabSettings(),
            2 => DrawLayoutSettings(),
            3 => DrawInteraction(),
            4 => DrawHistorySettings(),
            5 => DrawSettingsDiagnostics(),
            _ => false,
        };
        if (saveNotice.Length > 0) ImGui.TextWrapped(saveNotice);
    }
    private bool DrawSettingsDiagnostics() { DrawDiagnostics(); return false; }
}
