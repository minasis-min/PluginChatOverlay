using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace PluginChatOverlay;

public sealed partial class Plugin
{
    private void StartWindowDrag(ViewState state, Vector2 mouse, Vector2 position, bool alt)
    {
        state.PendingAltFrame = -1;
        state.HeaderDragging = true; state.AltDragging = alt;
        state.DragMouseStart = mouse; state.DragWindowStart = state.LastDragPosition = position;
        if (alt) { state.Selection.Clear(); state.ScrollbarDragging = false; copyInput.CancelRequest(); }
    }

    private static bool CanQueueAltPress()
    {
        var context = ImGui.GetCurrentContext();
        if (context.ActiveId != 0 || ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel)) return false;
        var current = ImGuiP.GetCurrentWindowRead().RootWindow;
        var hovered = context.HoveredWindow;
        if (hovered.IsNull) return true;
        var hoveredRoot = hovered.RootWindow;
        // The current window was transparent during NewFrame's hit test. Queue
        // only when it is above the old hovered window, never through another UI.
        for (var i = context.Windows.Size - 1; i >= 0; i--)
        {
            var root = context.Windows[i].RootWindow;
            if (root.Equals(current)) return true;
            if (root.Equals(hoveredRoot)) return false;
        }
        return false;
    }

    private void ResolvePendingAltDrag(string id)
    {
        var state = View(id);
        if (state.PendingAltFrame < 0 || state.PendingAltFrame == ImGui.GetFrameCount()) return;
        var previousFrame = state.PendingAltFrame;
        state.PendingAltFrame = -1;
        if (ImGui.GetFrameCount() != previousFrame + 1 || !AltMoveActive(id) || !ImGui.IsMouseDown(ImGuiMouseButton.Left)
            || resetGeometry || state.RequestedSize.HasValue || state.Host?.Tab != null && state.ResetGeometry) return;
        var context = ImGui.GetCurrentContext();
        // Validate the original press point, not the current mouse position: a
        // fast drag may already have left the old window rectangle by this frame.
        var root = TopInputRootAt(state.PendingAltMouse);
        if (root.IsNull || root.ID != state.PendingAltRootId) return;
        if (context.ActiveId != 0 && context.ActiveId != root.MoveId || (root.Pos - state.PendingAltWindow).LengthSquared() > 1) return;
        StartWindowDrag(state, state.PendingAltMouse, state.PendingAltWindow, true);
    }

    private static ImGuiWindowPtr TopInputRootAt(Vector2 point)
    {
        if (ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel)) return default;
        var windows = ImGui.GetCurrentContext().Windows;
        for (var i = windows.Size - 1; i >= 0; i--)
        {
            var window = windows[i];
            if ((!window.WasActive && !window.Active) || window.Hidden || (window.Flags & ImGuiWindowFlags.NoMouseInputs) != 0) continue;
            var bounds = window.OuterRectClipped;
            if (point.X >= bounds.Min.X && point.X < bounds.Max.X && point.Y >= bounds.Min.Y && point.Y < bounds.Max.Y)
                return window.RootWindow;
        }
        return default;
    }

    private Vector2 FinishWindowDrag(string id, ViewState state, Vector2 position)
    {
        state.HeaderDragging = state.AltDragging = false;
        state.PendingAltFrame = -1;
        if (!settings.SnapWindows || state.Window is not { } rect) return position;
        var viewport = ImGui.GetMainViewport();
        var targets = views.Where(p => p.Key != id && p.Value.Window != null && settings.IsWindowVisible(p.Key))
            .Select(p => p.Value.Window!.Value);
        return WindowSnap.Position(new(position.X, position.Y, rect.Width, rect.Height),
            new(viewport.WorkPos.X, viewport.WorkPos.Y, viewport.WorkSize.X, viewport.WorkSize.Y), targets, settings.SnapDistance);
    }
}
