using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiSeStringRenderer;
using Dalamud.Interface.Utility;

namespace PluginChatOverlay;

public sealed partial class Plugin
{
    private static readonly ImGuiCol[] StandardScrollbarColors = [ImGuiCol.ScrollbarBg, ImGuiCol.ScrollbarGrab, ImGuiCol.ScrollbarGrabHovered, ImGuiCol.ScrollbarGrabActive];
    private void DrawTabHistory(ChatTab tab, DisplayDocument[] docs, bool pass)
    {
        var state = View(tab.Id);
        var now = ImGui.GetTime();
        var wheel = ImGui.GetIO().MouseWheel != 0 && ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows);
        if (wheel) state.ScrollbarUntil = now + .65;
        if (!settings.UsesCustomScrollbar) { state.ScrollbarDragging = state.ScrollbarHovered = false; DrawStandardTabHistory(tab, docs, pass, state, now); return; }
        var start = ImGui.GetCursorPos();
        var screen = ImGui.GetCursorScreenPos();
        var available = ImGui.GetContentRegionAvail();
        var reserved = settings.EffectiveScrollbarDisplay == 0 ? 0 : settings.ScrollbarWidth + 3;
        var contentWidth = Math.Max(1, available.X - reserved);
        var windowPosition = ImGui.GetWindowPos(); var windowSize = ImGui.GetWindowSize();
        var bar = ScrollbarGeometry.Place(windowPosition, windowSize,
            screen + new Vector2(settings.ScrollbarLeft ? 0 : contentWidth + 3, 0), Math.Max(1, available.Y),
            settings.ScrollbarWidth, settings.ScrollbarPositionX, settings.ScrollbarOffsetX,
            settings.ScrollbarTopAdjustment ?? -settings.ScrollbarOffsetY,
            settings.ScrollbarBottomAdjustment ?? settings.ScrollbarOffsetY);
        UpdateScrollbarInput(state, bar.Position, bar.Height, pass || reserved == 0, now);
        var blockPointer = state.ScrollbarHovered || state.ScrollbarDragging;
        if (blockPointer) View(settings.WindowOf(tab)).HeaderDragging = false;
        if (settings.ScrollbarLeft) ImGui.SetCursorPos(start + new Vector2(reserved, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, settings.UseCustomWindowLayout ? Vector2.Zero : ImGui.GetStyle().WindowPadding);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        var child = ImGui.BeginChild("History-" + tab.Id, new(contentWidth, available.Y), false,
            ImGuiWindowFlags.NoScrollbar | (pass ? ImGuiWindowFlags.NoInputs : ImGuiWindowFlags.None));
        float scroll = 0, maximum = 0;
        try
        {
            if (child)
            {
                if (state.RequestedScroll is { } requested) { ImGui.SetScrollY(requested); state.RequestedScroll = null; }
                DrawRows(state, docs, pass, settings.WindowOf(tab), blockPointer);
                if (!pass && !blockPointer) DrawHistoryMenu(settings.WindowOf(tab));
                scroll = ImGui.GetScrollY(); maximum = ImGui.GetScrollMaxY();
                state.ScrollMaximum = maximum;
                if (state.PreviousScroll != scroll && (!state.Follow || wheel || ImGui.IsMouseDown(ImGuiMouseButton.Left))) state.ScrollbarUntil = now + .65;
                state.PreviousScroll = scroll;
                if (links.Notice.Length > 0) ImGui.TextWrapped(links.Notice);
            }
        }
        finally { ImGui.EndChild(); ImGui.PopStyleColor(); ImGui.PopStyleVar(); }
        if (reserved == 0) return;
        var after = ImGui.GetCursorPos();
        ImGui.PushClipRect(windowPosition, windowPosition + windowSize, false);
        try
        {
            ImGui.SetCursorScreenPos(bar.Position);
            DrawChatScrollbar(tab.Id, state, bar.Position, bar.Height, scroll, maximum, pass, now);
        }
        finally { ImGui.PopClipRect(); ImGui.SetCursorPos(after); }
    }

    private void DrawStandardTabHistory(ChatTab tab, DisplayDocument[] docs, bool pass, ViewState state, double now)
    {
        var show = settings.EffectiveScrollbarDisplay == 2 || now < state.ScrollbarUntil;
        foreach (var color in StandardScrollbarColors)
        {
            var value = ImGui.GetStyle().Colors[(int)color];
            if (color != ImGuiCol.ScrollbarBg)
                value = ImGui.ColorConvertU32ToFloat4(settings.ResolveScrollbarColor(ImGui.ColorConvertFloat4ToU32(value)));
            if (!show) value.W = 0;
            ImGui.PushStyleColor(color, value);
        }
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        var child = ImGui.BeginChild("History-" + tab.Id, Vector2.Zero, false,
            (settings.EffectiveScrollbarDisplay == 0 ? ImGuiWindowFlags.NoScrollbar : ImGuiWindowFlags.AlwaysVerticalScrollbar)
            | (pass ? ImGuiWindowFlags.NoInputs : ImGuiWindowFlags.None));
        try
        {
            if (!child) return;
            state.RequestedScroll = null;
            DrawRows(state, docs, pass, settings.WindowOf(tab));
            if (!pass) DrawHistoryMenu(settings.WindowOf(tab));
            var scroll = ImGui.GetScrollY();
            if (state.PreviousScroll != scroll && (!state.Follow || ImGui.GetIO().MouseWheel != 0 || ImGui.IsMouseDown(ImGuiMouseButton.Left))) state.ScrollbarUntil = now + .65;
            state.PreviousScroll = scroll;
            var mouse = ImGui.GetMousePos(); var pos = ImGui.GetWindowPos(); var size = ImGui.GetWindowSize();
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && mouse.X >= pos.X + size.X - ImGui.GetStyle().ScrollbarSize
                && mouse.X <= pos.X + size.X && mouse.Y >= pos.Y && mouse.Y <= pos.Y + size.Y) state.ScrollbarUntil = now + .65;
            if (links.Notice.Length > 0) ImGui.TextWrapped(links.Notice);
        }
        finally { ImGui.EndChild(); ImGui.PopStyleColor(5); }
    }
    private void DrawChatScrollbar(string tabId, ViewState state, Vector2 position, float height, float scroll, float maximum, bool pass, double now)
    {
        var width = settings.ScrollbarWidth;
        var style = ImGui.GetStyle();
        var thumb = ScrollbarGeometry.Thumb(position, width, height, scroll, maximum, style.GrabMinSize);
        var hovered = !pass && state.ScrollbarHovered; var active = !pass && state.ScrollbarDragging;
        ImGui.PushID(tabId);
        try
        {
            if (maximum <= 0 || settings.EffectiveScrollbarDisplay != 2 && now >= state.ScrollbarUntil && !active) return;
            var color = settings.ResolveScrollbarColor(ImGui.GetColorU32(active ? ImGuiCol.ScrollbarGrabActive : hovered ? ImGuiCol.ScrollbarGrabHovered : ImGuiCol.ScrollbarGrab));
            var draw = ImGui.GetWindowDrawList();
            if ((color >> 24) == 0) return;
            draw.AddRectFilled(position, position + new Vector2(width, height), ImGui.GetColorU32(ImGuiCol.ScrollbarBg), style.WindowRounding);
            var first = thumb.First; var last = thumb.Last;
            var rounding = style.ScrollbarRounding;
            if (settings.ScrollbarOutline)
                DrawScrollbarGlow(draw, first, last, color, rounding);
            draw.AddRectFilled(first, last, color, rounding);
        }
        finally { ImGui.PopID(); }
    }
    private void DrawScrollbarGlow(ImDrawListPtr draw, Vector2 first, Vector2 last, uint thumbColor, float rounding)
    {
        var glowColor = settings.ScrollbarOutlineColor;
        var alpha = (glowColor >> 24) * (thumbColor >> 24) / 255;
        if (alpha == 0) return;
        const int layers = 12;
        var spread = settings.ScrollbarOutlineWidth;
        var step = spread / (float)layers;
        for (var layer = layers - 1; layer >= 0; layer--)
        {
            var distance = (layer + .5f) * step;
            var fade = 1 - distance / spread;
            var layerAlpha = (uint)MathF.Round(alpha * fade * fade);
            if (layerAlpha == 0) continue;
            var expansion = new Vector2(distance);
            draw.AddRect(first - expansion, last + expansion,
                (glowColor & 0x00FFFFFFu) | (layerAlpha << 24),
                rounding + distance, ImDrawFlags.None, step);
        }
    }
    private void UpdateScrollbarInput(ViewState state, Vector2 position, float height, bool pass, double now)
    {
        var maximum = state.ScrollMaximum;
        if (pass || maximum <= 0) { state.ScrollbarHovered = state.ScrollbarDragging = false; return; }
        var mouse = ImGui.GetMousePos();
        state.ScrollbarHovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows)
            && mouse.X >= position.X && mouse.X < position.X + settings.ScrollbarWidth
            && mouse.Y >= position.Y && mouse.Y < position.Y + height;
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) state.ScrollbarDragging = false;
        if (state.ScrollbarHovered) state.ScrollbarUntil = now + .65;
        var thumb = ScrollbarGeometry.Thumb(position, settings.ScrollbarWidth, height,
            state.PreviousScroll, maximum, ImGui.GetStyle().GrabMinSize);
        var thumbY = thumb.First.Y - position.Y;
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            state.ScrollbarDragging = state.ScrollbarHovered;
            if (state.ScrollbarDragging)
            {
                var localY = mouse.Y - position.Y;
                state.ScrollbarDragOffset = localY >= thumbY && localY <= thumbY + thumb.ThumbHeight ? localY - thumbY : thumb.ThumbHeight / 2;
                ImGui.SetWindowFocus();
            }
        }
        if (state.ScrollbarDragging && thumb.Travel > 0)
        {
            state.RequestedScroll = Math.Clamp((mouse.Y - position.Y - thumb.TopInset - state.ScrollbarDragOffset) / thumb.Travel, 0, 1) * maximum;
            state.Follow = false; state.Jump = false; state.ScrollbarUntil = now + .65;
        }
        if (state.ScrollbarHovered || state.ScrollbarDragging) state.Selection.EndDrag();
    }
    private void DrawRows(ViewState state, DisplayDocument[] docs, bool pass, string windowId, bool blockPointer = false)
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
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X);
        var spacing = settings.ResolveMessageGap(ImGui.GetStyle().ItemSpacing.Y, ImGuiHelpers.GlobalScale);
        var style = new SeStringDrawParams { WrapWidth = width, FontSize = settings.FontSize, LineHeight = settings.LineHeightRatio, Opacity = 1, Color = originalChatColor,
            Edge = settings.UseTextOutline, ForceEdgeColor = settings.UseTextOutline, EdgeColor = settings.UseTextOutline ? settings.OutlineColor : null,
            EdgeStrength = settings.UseTextOutline ? settings.OutlineStrength : null };
        var lineHeight = MathF.Round(settings.FontSize * settings.LineHeightRatio);
        foreach (var doc in docs)
        {
            if (!state.Rows.TryGetValue(doc.Record.Id, out var row) || !ReferenceEquals(row.Document, doc))
            { row = new(doc); state.Rows[doc.Record.Id] = row; changed = true; }
            if (row.Width == width && row.FontSize == settings.FontSize && row.LineSpacing == settings.LineSpacing)
            {
                if (row.MessageGap != spacing)
                {
                    row.Height = ChatRowGeometry.ChangeSpacing(row.Height, row.MessageGap, spacing);
                    row.MessageGap = spacing; changed = true;
                }
                continue;
            }
            row.Glyphs = [];
            if (budget-- <= 0) continue;
            var measure = style; measure.TargetDrawList = default(ImDrawListPtr);
            measure.Font = ImGui.GetFont(); measure.ScreenOffset = Vector2.Zero;
            var result = ImGuiHelpers.SeStringWrapped(doc.Bytes, measure);
            row.Height = ChatRowGeometry.Advance(result.Size.Y, lineHeight, spacing); row.Width = width; row.FontSize = settings.FontSize; changed = true;
            row.LineSpacing = settings.LineSpacing;
            row.MessageGap = spacing;
        }
        var total = docs.Sum(d => state.Rows[d.Record.Id].Height);
        if (changed && !state.Follow && !state.ScrollbarDragging && state.AnchorId != 0)
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
        var clicked = !pass && !blockPointer && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && ImGui.IsWindowHovered()
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
                var result = ImGuiHelpers.SeStringWrapped(doc.Bytes, drawStyle, pass || blockPointer ? default : new ImGuiId("message"));
                row.Height = ChatRowGeometry.Advance(result.Size.Y, lineHeight, spacing);
                row.MessageGap = spacing;
                if (row.Glyphs.Length == 0 || row.Width != width || row.FontSize != settings.FontSize || row.LineSpacing != settings.LineSpacing) row.Glyphs = fragmentLayout.Copy(doc.Bytes, settings.FontSize, lineHeight);
                row.Width = width; row.FontSize = settings.FontSize;
                row.LineSpacing = settings.LineSpacing;
                var distance = Math.Max(rowScreen.Y - mouse.Y, Math.Max(0, mouse.Y - rowScreen.Y - row.Height));
                if (distance < nearestDistance) { nearestDistance = distance; nearest = new(doc.Record.Id, FragmentLayout.Caret(row.Glyphs, mouse - rowScreen, doc.Text.Length)); }
                if (!pass && !blockPointer && result.Clicked && result.InteractedPayloadOffset >= 0 && !state.Selection.Dragged) links.Activate(doc.Bytes, result.InteractedPayloadOffset);
                if (!pass && !blockPointer) DrawLogContextMenu(state, doc, docs, windowId);
                if (!pass && !blockPointer && settings.LinkTooltips && !state.Selection.Active && result.InteractedPayloadOffset >= 0)
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
        CaptureDrawCopyShortcut(state, pass);
        ImGui.SetCursorPos(new(origin.X, origin.Y + y)); ImGui.Dummy(new(1, 0));
        if (autoScroll && !state.Selection.Active) ImGui.SetScrollY(Math.Max(0, y - ImGui.GetWindowHeight() + ImGui.GetStyle().WindowPadding.Y * 2));
        if (!autoScroll && !state.Selection.Active && !state.ScrollbarDragging) state.Follow = ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 3;
        if (state.Follow) { state.ReadUpTo = docs.LastOrDefault()?.Record.Id ?? state.ReadUpTo; state.Unread = 0; }
    }
    private void CaptureDrawCopyShortcut(ViewState state, bool pass)
    {
        if (!settings.CopyWithCtrlC || pass || !ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows)
            || !ImGui.GetIO().KeyCtrl || !ImGui.IsKeyPressed(ImGuiKey.C, false)) return;
        keyState[0x43] = false;
        copyInput.Suppress(true, true, true, state);
        focusedView = state;
    }
}
