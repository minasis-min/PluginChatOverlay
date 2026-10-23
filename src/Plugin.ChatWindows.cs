using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace PluginChatOverlay;

public sealed partial class Plugin
{
    private static readonly ImGuiCol[] BackgroundColors = [ImGuiCol.TitleBg, ImGuiCol.TitleBgActive, ImGuiCol.TitleBgCollapsed, ImGuiCol.ScrollbarBg];
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
            && state.FilterPlugins.SequenceEqual(tab.Plugins)
            && state.FilterGameEnabled == tab.GameLogs.Enabled && tab.GameLogs.Rows.SetEquals(state.FilterGameRows);
        if (!ReferenceEquals(state.FilterSource, docs) || !sameRules)
        {
            state.FilterSource = docs; state.FilterAll = tab.IncludeAll; state.FilterUnknown = tab.IncludeUnknown;
            if (!sameRules)
            {
                state.FilterPlugins = tab.Plugins.ToArray();
                state.FilterGameEnabled = tab.GameLogs.Enabled; state.FilterGameRows = tab.GameLogs.Rows.ToArray();
            }
            state.FilteredDocuments = docs.Where(d => d.Record.Origin == ChatRecordOrigin.Game
                ? gameLogCatalog.Matches(tab.GameLogs, d.Record.Kind, d.Record.Source, d.Record.Target) : tab.Matches(d.Plugin)).ToArray();
        }
        return state.FilteredDocuments;
    }
    private void PushChatBackground()
    {
        // Black alpha compositing equals multiplication by (1 - alpha); no framebuffer pass is needed.
        var background = settings.DarkenBackground ? new Vector4(0, 0, 0, 1) : ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg];
        ImGui.PushStyleColor(ImGuiCol.WindowBg, background);
        ImGui.SetNextWindowBgAlpha(settings.UseBackgroundBlur ? 0 : settings.BackgroundAlpha);
        foreach (var color in BackgroundColors)
        {
            var value = ImGui.GetStyle().Colors[(int)color]; value.W = settings.BackgroundAlpha; ImGui.PushStyleColor(color, value);
        }
    }
    private void DrawMainWindow(DisplayDocument[] docs)
    {
        DragHeader("main"); UpdateGeometry("main", null);
        var pass = settings.ClickThrough || AltMoveActive("main");
        chatFocused |= !pass && ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        if (!settings.ShowTabs) DrawWithoutTabs("main", docs, pass);
        else if (BeginChatTabBar("ChatTabs", "main"))
        {
            try
            {
                foreach (var tab in settings.WindowTabs("main").ToArray())
                {
                    var state = View(tab.Id);
                    var label = tab.Name + (state.Unread > 0 ? $" ({state.Unread})" : "") + "###tab-" + tab.Id;
                    var selected = ImGui.BeginTabItem(label, View("main").RequestedTabId == tab.Id ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None);
                    DragHeader("main", ImGui.IsItemHovered());
                    using (popupStyle.PushFor("TabMenu-" + tab.Id))
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
                    try { DrawTabHistory(tab, TabDocuments(tab, docs), pass); }
                    finally { ImGui.EndTabItem(); }
                }
            }
            finally { EndChatTabBar(); View("main").RequestedTabId = ""; }
        }
        if (!AltMoveActive("main")) WindowMenu(null);
    }
    private void DrawDetached(ChatTab tab, DisplayDocument[] docs)
    {
        DragHeader(tab.Id); UpdateGeometry(tab.Id, tab);
        var pass = tab.ClickThrough || AltMoveActive(tab.Id);
        chatFocused |= !pass && ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        if (!settings.ShowTabs) DrawWithoutTabs(tab.Id, docs, pass);
        else if (BeginChatTabBar("DetachedTabs", tab.Id))
        {
            try
            {
                foreach (var childTab in settings.WindowTabs(tab.Id).ToArray())
                {
                    var state = View(childTab.Id);
                    var selected = ImGui.BeginTabItem(childTab.Name + (state.Unread > 0 ? $" ({state.Unread})" : "") + "###detached-" + childTab.Id,
                        View(tab.Id).RequestedTabId == childTab.Id ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None);
                    DragHeader(tab.Id, ImGui.IsItemHovered());
                    using (popupStyle.PushFor("DetachedTabMenu-" + childTab.Id))
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
                    try { DrawTabHistory(childTab, TabDocuments(childTab, docs), pass); }
                    finally { ImGui.EndTabItem(); }
                }
            }
            finally { EndChatTabBar(); View(tab.Id).RequestedTabId = ""; }
        }
        if (!AltMoveActive(tab.Id)) WindowMenu(tab);
    }
    private bool BeginChatTabBar(string label, string windowId)
    {
        ImGui.BeginDisabled(AltMoveActive(windowId));
        try
        {
            if (ImGui.BeginTabBar(label)) return true;
        }
        catch { ImGui.EndDisabled(); throw; }
        ImGui.EndDisabled();
        return false;
    }
    private static void EndChatTabBar()
    {
        try { ImGui.EndTabBar(); }
        finally { ImGui.EndDisabled(); }
    }
    private void SetTabBarVisible(bool visible)
    {
        settings.ShowTabs = visible;
        foreach (var state in views.Values)
        {
            state.HeaderDragging = false;
            state.PendingAltFrame = -1;
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

    private void DragHeader(string id, bool tabHovered = false)
    {
        var state = View(id);
        if (!settings.AllowMove || state.Host is { } host && (host.IsPinned || host.IsClickthrough)) { state.HeaderDragging = false; return; }
        var mouse = ImGui.GetMousePos(); var pos = ImGui.GetWindowPos(); var size = ImGui.GetWindowSize();
        var titleHovered = settings.ShowWindowTitle && ImGui.IsWindowHovered() && mouse.Y >= pos.Y && mouse.Y < pos.Y + ImGui.GetFrameHeight()
            && mouse.X >= pos.X + 25 && mouse.X < pos.X + size.X - ImGui.GetFrameHeight() * 3;
        var alt = AltMoveActive(id);
        var altHovered = alt && ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows)
            && (!settings.ShowWindowTitle || mouse.Y >= pos.Y + ImGui.GetFrameHeight() || titleHovered);
        if (!state.HeaderDragging && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            if (alt ? altHovered : titleHovered || tabHovered) StartWindowDrag(state, mouse, pos, alt);
            else if (alt && (state.Host?.Tab?.ClickThrough ?? settings.ClickThrough)
                && mouse.X >= pos.X && mouse.X < pos.X + size.X && mouse.Y >= pos.Y && mouse.Y < pos.Y + size.Y
                && (!settings.ShowWindowTitle || mouse.Y >= pos.Y + ImGui.GetFrameHeight()) && CanQueueAltPress())
            {
                state.PendingAltFrame = ImGui.GetFrameCount();
                state.PendingAltRootId = ImGuiP.GetCurrentWindowRead().RootWindow.ID;
                state.PendingAltMouse = mouse; state.PendingAltWindow = pos;
            }
        }
    }
    private bool AltMoveActive(string id) => settings.AltDragMove && settings.AllowMove
        && !(View(id).Host?.IsPinned ?? settings.Pinned) && ImGui.GetIO().KeyAlt;
    // All window position changes occur before Begin: background, tabs and text share one origin.
    private void ApplyHeaderDrag(string id)
    {
        var state = View(id);
        if (!state.HeaderDragging) { state.AltDragging = false; return; }
        var altEnded = state.AltDragging && !AltMoveActive(id);
        if (!settings.AllowMove || state.Host is { } host && (host.IsPinned || host.IsClickthrough && !altEnded))
        { state.HeaderDragging = state.AltDragging = false; return; }
        // A modifier release must finish at the last rendered origin, rather than
        // following mouse movement that arrived after Alt was released.
        var pos = altEnded ? state.Window is { } last ? new Vector2(last.X, last.Y) : state.LastDragPosition
            : WindowDrag.Position(state.DragWindowStart, state.DragMouseStart, ImGui.GetMousePos());
        if (altEnded || !ImGui.IsMouseDown(ImGuiMouseButton.Left)) pos = FinishWindowDrag(id, state, pos);
        state.LastDragPosition = pos;
        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
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
