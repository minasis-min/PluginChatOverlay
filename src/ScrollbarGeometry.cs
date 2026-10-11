using System.Numerics;

namespace PluginChatOverlay;

internal static class ScrollbarGeometry
{
    internal static (Vector2 First, Vector2 Last, float Travel, float TopInset, float ThumbHeight) Thumb(
        Vector2 position, float width, float height, float scroll, float maximum, float minimumGrab)
    {
        // Standard ImGui scrollbar geometry: inset grab within its track, not a full-width pill.
        var insetX = Math.Clamp(MathF.Truncate((width - 2) / 2), 0, 3);
        var insetY = Math.Clamp(MathF.Truncate((height - 2) / 2), 0, 3);
        var length = Math.Max(1, height - insetY * 2);
        var thumbHeight = Math.Clamp(length * height / Math.Max(1, height + maximum), Math.Min(length, minimumGrab), length);
        var travel = length - thumbHeight;
        var offset = maximum > 0 ? Math.Clamp(scroll / maximum, 0, 1) * travel : 0;
        var first = position + new Vector2(insetX, insetY + offset);
        return (first, first + new Vector2(width - insetX * 2, thumbHeight), travel, insetY, thumbHeight);
    }
    internal static (Vector2 Position, float Height) Place(Vector2 windowPosition, Vector2 windowSize,
        Vector2 defaultPosition, float height, float width, int? absoluteX, int offsetX, int topAdjustment, int bottomAdjustment)
    {
        height = Math.Clamp(height, 1, Math.Max(1, windowSize.Y));
        var x = absoluteX ?? defaultPosition.X - windowPosition.X + offsetX;
        var defaultY = defaultPosition.Y - windowPosition.Y;
        var top = Math.Clamp(defaultY - topAdjustment, 0, Math.Max(0, windowSize.Y - 1));
        var bottom = Math.Clamp(defaultY + height + bottomAdjustment, top + 1, Math.Max(top + 1, windowSize.Y));
        return (windowPosition + new Vector2(Math.Clamp(x, 0, Math.Max(0, windowSize.X - width)),
            top), bottom - top);
    }
}
