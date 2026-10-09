using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiSeStringRenderer;
using Dalamud.Interface.Utility;

namespace PluginChatOverlay;

public sealed partial class Plugin
{
    private void DrawTabHistory(ChatTab tab, DisplayDocument[] docs, bool pass)
    {
        var state = View(tab.Id);
        var now = ImGui.GetTime();
        var wheel = ImGui.GetIO().MouseWheel != 0 && ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows);
        if (wheel) state.ScrollbarUntil = now + .65;
        var showScrollbar = now < state.ScrollbarUntil;
        foreach (var color in ScrollbarColors)
        {
            var value = ImGui.GetStyle().Colors[(int)color]; if (!showScrollbar) value.W = 0; ImGui.PushStyleColor(color, value);
        }
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        var child = ImGui.BeginChild("History-" + tab.Id, Vector2.Zero, false, ImGuiWindowFlags.AlwaysVerticalScrollbar | (pass ? ImGuiWindowFlags.NoInputs : ImGuiWindowFlags.None));
        try
        {
            if (!child) return;
            DrawRows(state, docs, pass, settings.WindowOf(tab));
            if (!pass && ImGui.BeginPopupContextWindow("HistoryMenu", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
            {
                try { TabBarMenu(settings.WindowOf(tab)); if (ImGui.MenuItem("設定を開く")) configOpen = true; }
                finally { ImGui.EndPopup(); }
            }
            var scroll = ImGui.GetScrollY();
            if (state.PreviousScroll != scroll && (!state.Follow || wheel || ImGui.IsMouseDown(ImGuiMouseButton.Left))) state.ScrollbarUntil = now + .65;
            state.PreviousScroll = scroll;
            var mouse = ImGui.GetMousePos(); var pos = ImGui.GetWindowPos(); var size = ImGui.GetWindowSize();
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && mouse.X >= pos.X + size.X - ImGui.GetStyle().ScrollbarSize && mouse.X <= pos.X + size.X && mouse.Y >= pos.Y && mouse.Y <= pos.Y + size.Y) state.ScrollbarUntil = now + .65;
            if (links.Notice.Length > 0) ImGui.TextWrapped(links.Notice);
        }
        finally { ImGui.EndChild(); ImGui.PopStyleColor(5); }
    }
    private void DrawRows(ViewState state, DisplayDocument[] docs, bool pass, string windowId)
    {
        if (!pass && ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows)) focusedView = state;
        var changed = false; var budget = 64;
        if (!ReferenceEquals(state.Documents, docs))
        {
            state.Documents = docs; state.DocumentIds = docs.Select(d => d.Record.Id).ToArray();
            state.DocumentIndices = state.DocumentIds.Select((id, index) => (id, index)).ToDictionary(p => p.id, p => p.index);
            state.ActiveIds = state.DocumentIds.ToHashSet();
            foreach (var id in state.Rows.Keys.Where(id => !state.ActiveIds.Contains(id)).ToArray()) state.Rows.Remove(id);
            changed = true;
        }
        var ids = state.DocumentIds; var active = state.ActiveIds;
        if (state.Selection.Start is { } startPoint && !active.Contains(startPoint.RecordId) || state.Selection.End is { } endPoint && !active.Contains(endPoint.RecordId)) state.Selection.Clear();
        if (state.AnchorId != 0 && !active.Contains(state.AnchorId)) { state.AnchorId = docs.FirstOrDefault()?.Record.Id ?? 0; state.AnchorOffset = 0; }
        if (state.Follow && ImGui.GetScrollY() < ImGui.GetScrollMaxY() - 3) state.Follow = false;
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X); var spacing = ImGui.GetStyle().ItemSpacing.Y;
        var style = new SeStringDrawParams { WrapWidth = width, FontSize = settings.FontSize, LineHeight = settings.LineHeightRatio, Opacity = 1, Color = originalChatColor };
        var lineHeight = MathF.Round(settings.FontSize * settings.LineHeightRatio);
        foreach (var doc in docs)
        {
            if (!state.Rows.TryGetValue(doc.Record.Id, out var row) || !ReferenceEquals(row.Document, doc))
            { row = new(doc); state.Rows[doc.Record.Id] = row; changed = true; }
            if (row.Width == width && row.FontSize == settings.FontSize && row.LineSpacing == settings.LineSpacing) continue;
            row.Glyphs = [];
            if (budget-- <= 0) continue;
            var measure = style; measure.TargetDrawList = default(ImDrawListPtr);
            measure.Font = ImGui.GetFont(); measure.ScreenOffset = Vector2.Zero;
            var result = ImGuiHelpers.SeStringWrapped(doc.Bytes, measure);
            row.Height = ChatRowGeometry.Advance(result.Size.Y, lineHeight, spacing); row.Width = width; row.FontSize = settings.FontSize; changed = true;
            row.LineSpacing = settings.LineSpacing;
        }
        var total = docs.Sum(d => state.Rows[d.Record.Id].Height);
        if (changed && !state.Follow && state.AnchorId != 0)
        {
            var before = 0f;
            foreach (var doc in docs) { if (doc.Record.Id >= state.AnchorId) break; before += state.Rows[doc.Record.Id].Height; }
            ImGui.SetScrollY(Math.Max(0, before + state.AnchorOffset));
        }
        var autoScroll = (state.Follow && changed) || state.Jump;
        state.Jump = false;
        if (autoScroll) ImGui.SetScrollY(Math.Max(0, total - ImGui.GetWindowHeight() + ImGui.GetStyle().WindowPadding.Y * 2));
        var scroll = ImGui.GetScrollY(); var viewHeight = ImGui.GetWindowHeight();
        var origin = ImGui.GetCursorPos(); var screenStart = ImGui.GetCursorScreenPos(); var y = 0f; var anchored = false;
        var mouse = ImGui.GetMousePos(); SelectionPoint? nearest = null; var nearestDistance = float.MaxValue;
        // A release can occur while this tab/window is hidden and miss its release frame.
        if (state.Selection.Active && !ImGui.IsMouseDown(ImGuiMouseButton.Left) && !ImGui.IsMouseReleased(ImGuiMouseButton.Left)) state.Selection.EndDrag();
        if (pass && state.Selection.Active) state.Selection.Clear();
        if (state.Selection.Active && (ImGui.IsMouseDown(ImGuiMouseButton.Left) || ImGui.IsMouseReleased(ImGuiMouseButton.Left)) && (mouse - state.Selection.PressPosition).LengthSquared() > 16)
            state.Selection.Dragged = true;
        var clicked = !pass && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && ImGui.IsWindowHovered()
            && mouse.X >= screenStart.X && mouse.X < screenStart.X + width;
        if (clicked) state.Selection.Clear();
        foreach (var doc in docs)
        {
            var row = state.Rows[doc.Record.Id];
            if (!anchored && y + row.Height > scroll) { state.AnchorId = doc.Record.Id; state.AnchorOffset = scroll - y; anchored = true; }
            if (y + row.Height < scroll - settings.FontSize || y > scroll + viewHeight)
            { row.Glyphs = []; y += row.Height; continue; }
            ImGui.SetCursorPos(new(origin.X, origin.Y + y)); var rowScreen = ImGui.GetCursorScreenPos();
            ImGui.PushID(doc.Record.Id.ToString());
            try
            {
                var drawStyle = style;
                var result = ImGuiHelpers.SeStringWrapped(doc.Bytes, drawStyle, pass ? default : new ImGuiId("message"));
                row.Height = ChatRowGeometry.Advance(result.Size.Y, lineHeight, spacing);
                if (row.Glyphs.Length == 0 || row.Width != width || row.FontSize != settings.FontSize || row.LineSpacing != settings.LineSpacing) row.Glyphs = fragmentLayout.Copy(doc.Bytes, settings.FontSize, lineHeight);
                row.Width = width; row.FontSize = settings.FontSize;
                row.LineSpacing = settings.LineSpacing;
                var distance = Math.Max(rowScreen.Y - mouse.Y, Math.Max(0, mouse.Y - rowScreen.Y - row.Height));
                if (distance < nearestDistance) { nearestDistance = distance; nearest = new(doc.Record.Id, FragmentLayout.Caret(row.Glyphs, mouse - rowScreen, doc.Text.Length)); }
                if (!pass && result.Clicked && result.InteractedPayloadOffset >= 0 && !state.Selection.Dragged) links.Activate(doc.Bytes, result.InteractedPayloadOffset);
                if (!pass && ImGui.BeginPopupContextItem("Copy"))
                {
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
                if (!pass && settings.LinkTooltips && !state.Selection.Active && result.InteractedPayloadOffset >= 0)
                    links.DrawTooltip(doc.Bytes, result.InteractedPayloadOffset);
                if (state.Selection.Range(state.DocumentIndices, doc.Record.Id, doc.Text.Length, out var from, out var to))
                {
                    var drawList = ImGui.GetWindowDrawList();
                    foreach (var glyph in row.Glyphs)
                        if (glyph.Character < to && glyph.Character + glyph.Length > from)
                            drawList.AddRectFilled(rowScreen + glyph.Position, rowScreen + glyph.Position + glyph.Size, 0x664E9EFF);
                }
            }
            finally { ImGui.PopID(); }
            y += row.Height;
        }
        if (!pass && nearest != null)
        {
            if (clicked && nearestDistance == 0 && fragmentLayout.Status.Length == 0)
            { state.Selection.Start = state.Selection.End = nearest; state.Selection.PressPosition = mouse; state.Selection.Active = true; }
            if (state.Selection.Active && ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                state.Selection.End = nearest;
                if ((mouse - state.Selection.PressPosition).LengthSquared() > 16) state.Selection.Dragged = true;
                if (state.Selection.Dragged)
                {
                    state.Follow = false;
                    var top = ImGui.GetWindowPos().Y; var bottom = top + viewHeight;
                    if (mouse.Y < top + 15) ImGui.SetScrollY(Math.Max(0, scroll - settings.FontSize));
                    if (mouse.Y > bottom - 15) ImGui.SetScrollY(scroll + settings.FontSize);
                }
            }
            if (state.Selection.Active && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            { state.Selection.End = nearest; state.Selection.Active = false; if (state.Selection.Dragged && settings.CopyOnDragRelease) CopySelection(state, docs); }
        }
        if (!pass && ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && ImGui.GetIO().KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.C, false))
        {
            keyState[0x43] = false;
            copyInput.Suppress(true, true, true, state);
            focusedView = state;
        }
        ImGui.SetCursorPos(new(origin.X, origin.Y + y)); ImGui.Dummy(new(1, 0));
        if (autoScroll && !state.Selection.Active) ImGui.SetScrollY(Math.Max(0, y - ImGui.GetWindowHeight() + ImGui.GetStyle().WindowPadding.Y * 2));
        if (!autoScroll && !state.Selection.Active) state.Follow = ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 3;
        if (state.Follow) { state.ReadUpTo = docs.LastOrDefault()?.Record.Id ?? state.ReadUpTo; state.Unread = 0; }
    }
}
