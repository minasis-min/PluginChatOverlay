using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using MinasisUI.Workspace;
namespace PluginChatOverlay;
public sealed partial class Plugin
{
    private static WorkspaceUi CreateWorkspace(WorkspacePreferences? preferences = null) => new(new("PluginChatOverlay.Config.Workspace", "Plugin Chat Overlay 設定", "Plugin Chat Overlay", "1.0.10",
    [
        new("basic", "基本", FontAwesomeIcon.Cog, []),
        new("tabs", "タブ", FontAwesomeIcon.Comments, [new("manage", "管理"), new("plugins", "プラグイン通知"), new("game", "ゲームログ"), new("window", "ウィンドウ")]),
        new("layout", "レイアウト", FontAwesomeIcon.PaintBrush, [new("background", "外観"), new("scroll", "スクロールバー"), new("text", "文字"), new("links", "リンク・ボタン"), new("toasts", "トースト通知"), new("reset", "リセット") { Icon = FontAwesomeIcon.Undo }]),
        new("interaction", "操作", FontAwesomeIcon.MousePointer, []),
        new("history", "履歴", FontAwesomeIcon.History, []),
        new("diagnostics", "診断・共存", FontAwesomeIcon.InfoCircle, []),
    ]), preferences: preferences);
    private void InitializeWorkspace()
    {
        workspaceControlTheme=ui.Theme;
        workspaceWindow = new(workspace, DrawWorkspaceContent);
        workspaceWindow.ForceMainWindow = false;
        settingsWindows.AddWindow(workspaceWindow);
        workspaceFonts = new(pi);
        workspace.SidebarAfterNavigation = () => { workspace.Divider(); settingsChanged |= DrawUiStyleSettings(true); };
        workspace.Persistence.SaveRequested += request =>
        {
            settings.WorkspacePreferences = request.Preferences; Save();
            if (saveNotice.Length == 0) workspace.Persistence.AcknowledgeSaved(request.Revision);
        };
    }
    private void DrawWorkspaceConfig()
    {
        if (workspaceWindow == null) return;
        var previousTheme=ui.Theme;
        ui.Theme=workspaceControlTheme ?? previousTheme;
        try
        {
            if (workspaceWindow.IsOpen != configOpen) { workspace.IsOpen = configOpen; workspaceWindow.IsOpen = configOpen; }

            if (focusConfig) { workspaceWindow.RequestFocus = true; focusConfig = false; }
            if (resetConfigSize) { workspace.State.ExpandedSize = new(settings.ConfigWidth, settings.ConfigHeight); workspaceWindow.Size = workspace.State.ExpandedSize; resetConfigSize = false; }
            settingsChanged = false; settingsWindows.Draw(); configOpen = workspaceWindow.IsOpen;
        }
        finally { ui.Theme=previousTheme; }
    }

    private bool UpdateWorkspaceConfigSize()
    {
        if (workspace.IsCollapsed || ImGui.IsMouseDown(ImGuiMouseButton.Left)) return false;
        // Called inside a content pane: persist the root workspace, never that child.
        var size = workspace.ExpandedSize;
        if (Math.Abs(size.X - settings.ConfigWidth) < 1 && Math.Abs(size.Y - settings.ConfigHeight) < 1) return false;
        settings.ConfigWidth = size.X; settings.ConfigHeight = size.Y; return true;
    }
    private void DrawWorkspaceContent()
    {
        if (selectChatTabSettings) { workspace.Select("tabs"); selectChatTabSettings = false; }
        settingsChanged |= UpdateWorkspaceConfigSize();
        var before = RenderSettingsState.Capture(settings);
        settingsPage = workspace.Page;
        workspace.PageScrollPane("settings-page", () =>
        {
            DrawSettingsPage();
        });
        if (settingsChanged) { UpdateRenderSettings(before); ApplyUiVisibility(); Save(); }
    }
}
