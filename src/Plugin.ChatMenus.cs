using Dalamud.Bindings.ImGui;

namespace PluginChatOverlay;

public sealed partial class Plugin
{
    private void TabBarMenu(string windowId)
    {
        if (ImGui.MenuItem(settings.ShowTabs ? "タブバーを隠す" : "タブバーを表示")) { SetTabBarVisible(!settings.ShowTabs); Save(); }
        if (!ImGui.BeginMenu("タブを切り替え")) return;
        try
        {
            foreach (var tab in settings.WindowTabs(windowId))
                if (ImGui.MenuItem(tab.Name + "###switch-" + tab.Id, "", View(windowId).ActiveTabId == tab.Id))
                { View(windowId).ActiveTabId = tab.Id; View(windowId).RequestedTabId = tab.Id; }
        }
        finally { ImGui.EndMenu(); }
    }

    private void WindowMenu(ChatTab? tab)
    {
        using var menuStyle = popupStyle.PushFor("WindowMenu");
        if (!ImGui.BeginPopupContextWindow("WindowMenu", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems)) return;
        try
        {
            if (ImGui.MenuItem("設定を開く")) configOpen = true;
            TabBarMenu(tab?.Id ?? "main");
            if (tab != null && ImGui.MenuItem("他のウィンドウにタブとしてまとめる"))
            { settings.Attach(tab, settings.Tabs.FirstOrDefault(t => t.Detached && t.Id != tab.Id)?.Id ?? "main"); tab.Visible = true; Save(); }
        }
        finally { ImGui.EndPopup(); }
    }

    private void DrawHistoryMenu(string windowId)
    {
        using var menuStyle = popupStyle.PushFor("HistoryMenu");
        if (!ImGui.BeginPopupContextWindow("HistoryMenu", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems)) return;
        try
        {
            TabBarMenu(windowId);
            if (ImGui.MenuItem("設定を開く")) configOpen = true;
        }
        finally { ImGui.EndPopup(); }
    }

    private void DrawLogContextMenu(ViewState state, DisplayDocument doc, DisplayDocument[] docs, string windowId)
    {
        using var menuStyle = popupStyle.PushFor("Copy");
        if (!ImGui.BeginPopupContextItem("Copy")) return;
        try
        {
            if (ImGui.MenuItem("このログをコピー")) ImGui.SetClipboardText(doc.Text);
            if (state.Selection.Dragged && ImGui.MenuItem("選択した文章をコピー")) CopySelection(state, docs);
            if (ImGui.MenuItem("最下部へ移動")) { state.Follow = true; state.Jump = true; }
            if (ImGui.MenuItem("設定を開く")) configOpen = true;
            TabBarMenu(windowId);
        }
        finally { ImGui.EndPopup(); }
    }
}
