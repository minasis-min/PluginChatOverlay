using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace PluginChatOverlay;

public sealed partial class Plugin
{
    private static readonly ImGuiCol[] BackgroundColors = [ImGuiCol.TitleBg, ImGuiCol.TitleBgActive, ImGuiCol.TitleBgCollapsed, ImGuiCol.ScrollbarBg];
    private static readonly ImGuiCol[] ScrollbarColors = [ImGuiCol.ScrollbarBg, ImGuiCol.ScrollbarGrab, ImGuiCol.ScrollbarGrabHovered, ImGuiCol.ScrollbarGrabActive];
    private void DrawChatWindows()
    {
        var snapshot = history.RenderSnapshot();
        if (!ReferenceEquals(snapshot, renderedSnapshot) || renderedRevision != presentationRevision)
        {
            var ids = snapshot.Select(r => r.Id).ToHashSet();
            foreach (var id in documents.Keys.Where(id => !ids.Contains(id)).ToArray()) documents.Remove(id);
            renderedDocuments = snapshot.Select(Document).ToArray();
            renderedSnapshot = snapshot; renderedRevision = presentationRevision;
        }
        foreach (var id in views.Keys.Where(id => id != "main" && settings.Tabs.All(t => t.Id != id)).ToArray()) views.Remove(id);
        var docs = renderedDocuments;
        foreach (var tab in settings.Tabs)
        {
            var view = View(tab.Id);
            var filtered = TabDocuments(tab, docs);
            if (!ReferenceEquals(view.UnreadDocuments, filtered) || view.UnreadReadUpTo != view.ReadUpTo)
            {
                view.Unread = filtered.Count(d => d.Record.Id > view.ReadUpTo);
                view.UnreadDocuments = filtered; view.UnreadReadUpTo = view.ReadUpTo;
            }
        }
        DrawStandardWindows(docs);
    }
    private DisplayDocument[] TabDocuments(ChatTab tab, DisplayDocument[] docs)
    {
        var state = View(tab.Id);
        var sameRules = state.FilterAll == tab.IncludeAll && state.FilterUnknown == tab.IncludeUnknown
            && state.FilterPlugins.SequenceEqual(tab.Plugins);
        if (!ReferenceEquals(state.FilterSource, docs) || !sameRules)
        {
            state.FilterSource = docs; state.FilterAll = tab.IncludeAll; state.FilterUnknown = tab.IncludeUnknown;
            if (!sameRules) state.FilterPlugins = tab.Plugins.ToArray();
            state.FilteredDocuments = tab.IncludeAll ? docs : docs.Where(d => tab.Matches(d.Plugin)).ToArray();
        }
        return state.FilteredDocuments;
    }
    private void PushChatBackground()
    {
        ImGui.SetNextWindowBgAlpha(settings.BackgroundAlpha);
        foreach (var color in BackgroundColors)
        {
            var value = ImGui.GetStyle().Colors[(int)color]; value.W = settings.BackgroundAlpha; ImGui.PushStyleColor(color, value);
        }
    }
    private void DrawMainWindow(DisplayDocument[] docs)
    {
        DragHeader("main"); UpdateGeometry("main", null);
        chatFocused |= !settings.ClickThrough && ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        if (!settings.ShowTabs) DrawWithoutTabs("main", docs, settings.ClickThrough);
        else if (ImGui.BeginTabBar("ChatTabs"))
        {
            try
            {
                foreach (var tab in settings.WindowTabs("main").ToArray())
                {
                    var state = View(tab.Id);
                    var label = tab.Name + (state.Unread > 0 ? $" ({state.Unread})" : "") + "###tab-" + tab.Id;
                    var selected = ImGui.BeginTabItem(label, View("main").RequestedTabId == tab.Id ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None);
                    DragHeader("main", ImGui.IsItemHovered());
                    if (ImGui.BeginPopupContextItem("TabMenu-" + tab.Id))
                    {
                        try
                        {
                            if (ImGui.MenuItem("ウィンドウ化")) { tab.Detached = true; tab.Visible = true; state.ResetGeometry = true; Save(); }
                            if (ImGui.MenuItem("このタブの設定")) OpenTabSettings(tab.Id);
                            if (ImGui.MenuItem("タブバーを隠す")) { SetTabBarVisible(false); Save(); }
                        }
                        finally { ImGui.EndPopup(); }
                    }
                    if (!selected) continue;
                    View("main").ActiveTabId = tab.Id;
                    try { DrawTabHistory(tab, TabDocuments(tab, docs), settings.ClickThrough); }
                    finally { ImGui.EndTabItem(); }
                }
            }
            finally { ImGui.EndTabBar(); View("main").RequestedTabId = ""; }
        }
        WindowMenu(null);
    }
    private void DrawDetached(ChatTab tab, DisplayDocument[] docs)
    {
        DragHeader(tab.Id); UpdateGeometry(tab.Id, tab);
        chatFocused |= !tab.ClickThrough && ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        if (!settings.ShowTabs) DrawWithoutTabs(tab.Id, docs, tab.ClickThrough);
        else if (ImGui.BeginTabBar("DetachedTabs"))
        {
            try
            {
                foreach (var childTab in settings.WindowTabs(tab.Id).ToArray())
                {
                    var state = View(childTab.Id);
                    var selected = ImGui.BeginTabItem(childTab.Name + (state.Unread > 0 ? $" ({state.Unread})" : "") + "###detached-" + childTab.Id,
                        View(tab.Id).RequestedTabId == childTab.Id ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None);
                    DragHeader(tab.Id, ImGui.IsItemHovered());
                    if (ImGui.BeginPopupContextItem("DetachedTabMenu-" + childTab.Id))
                    {
                        try
                        {
                            if (!childTab.Detached && ImGui.MenuItem("ウィンドウ化"))
                            { childTab.Detached = true; childTab.ParentWindowId = ""; childTab.Visible = true; state.ResetGeometry = true; Save(); }
                            if (ImGui.MenuItem("このタブの設定")) OpenTabSettings(childTab.Id);
                            if (ImGui.MenuItem("タブバーを隠す")) { SetTabBarVisible(false); Save(); }
                        }
                        finally { ImGui.EndPopup(); }
                    }
                    if (!selected) continue;
                    View(tab.Id).ActiveTabId = childTab.Id;
                    try { DrawTabHistory(childTab, TabDocuments(childTab, docs), tab.ClickThrough); }
                    finally { ImGui.EndTabItem(); }
                }
            }
            finally { ImGui.EndTabBar(); View(tab.Id).RequestedTabId = ""; }
        }
        WindowMenu(tab);
    }
    private void SetTabBarVisible(bool visible)
    {
        settings.ShowTabs = visible;
        foreach (var state in views.Values)
        {
            state.HeaderDragging = false;
            if (state.ActiveTabId.Length > 0) state.RequestedTabId = state.ActiveTabId;
        }
    }
    private void DrawWithoutTabs(string windowId, DisplayDocument[] docs, bool pass)
    {
        var state = View(windowId);
        var tabs = settings.WindowTabs(windowId).ToArray();
        var active = tabs.FirstOrDefault(t => t.Id == state.ActiveTabId) ?? tabs.FirstOrDefault();
        if (active == null) return;
        state.ActiveTabId = active.Id;
        DrawTabHistory(active, TabDocuments(active, docs), pass);
    }
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
    private void DragHeader(string id, bool tabHovered = false)
    {
        var state = View(id);
        if (!settings.AllowMove || state.Host is { } host && (host.IsPinned || host.IsClickthrough)) { state.HeaderDragging = false; return; }
        var mouse = ImGui.GetMousePos(); var pos = ImGui.GetWindowPos(); var size = ImGui.GetWindowSize();
        var titleHovered = settings.ShowWindowTitle && ImGui.IsWindowHovered() && mouse.Y >= pos.Y && mouse.Y < pos.Y + ImGui.GetFrameHeight()
            && mouse.X >= pos.X + 25 && mouse.X < pos.X + size.X - ImGui.GetFrameHeight() * 3;
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && (titleHovered || tabHovered))
        {
            state.HeaderDragging = true; state.DragMouseStart = mouse; state.DragWindowStart = pos;
        }
    }
    // All window position changes occur before Begin: background, tabs and text share one origin.
    private void ApplyHeaderDrag(string id)
    {
        var state = View(id);
        if (!state.HeaderDragging) return;
        if (!settings.AllowMove || state.Host is { } host && (host.IsPinned || host.IsClickthrough))
        { state.HeaderDragging = false; return; }
        var pos = WindowDrag.Position(state.DragWindowStart, state.DragMouseStart, ImGui.GetMousePos());
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            state.HeaderDragging = false;
            if (settings.SnapWindows && state.Window is { } rect)
            {
                var viewport = ImGui.GetMainViewport();
                var targets = views.Where(p => p.Key != id && p.Value.Window != null && settings.IsWindowVisible(p.Key))
                    .Select(p => p.Value.Window!.Value);
                pos = WindowSnap.Position(new(pos.X, pos.Y, rect.Width, rect.Height),
                    new(viewport.WorkPos.X, viewport.WorkPos.Y, viewport.WorkSize.X, viewport.WorkSize.Y), targets, settings.SnapDistance);
            }
        }
        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
    }
    private void WindowMenu(ChatTab? tab)
    {
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
    private void UpdateGeometry(string id, ChatTab? tab)
    {
        var state = View(id); var pos = ImGui.GetWindowPos(); var size = ImGui.GetWindowSize();
        state.Window = new(pos.X, pos.Y, size.X, size.Y);
        var savedPos = tab == null ? settings.Position : new Vector2(tab.X, tab.Y);
        var savedSize = tab == null ? settings.Size : new Vector2(tab.Width, tab.Height);
        if ((pos - savedPos).LengthSquared() <= 1 && (size - savedSize).LengthSquared() <= 1) return;
        if (tab == null) { settings.X = pos.X; settings.Y = pos.Y; settings.Width = size.X; settings.Height = size.Y; }
        else { tab.X = pos.X; tab.Y = pos.Y; tab.Width = size.X; tab.Height = size.Y; }
        geometryDirtyAt = Environment.TickCount64;
    }

}
