using System.Numerics;

namespace PluginChatOverlay;

internal static class WindowDrag
{
    public static Vector2 Position(Vector2 windowStart, Vector2 mouseStart, Vector2 mouse)
    {
        var pos = windowStart + mouse - mouseStart;
        return new(MathF.Round(pos.X), MathF.Round(pos.Y));
    }
}
