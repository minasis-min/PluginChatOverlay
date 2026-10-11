using System.Numerics;

namespace PluginChatOverlay;

internal readonly record struct NativeButtonLayout(Vector2 Position, float Height, float FontSize, float Spacing)
{
    public static NativeButtonLayout Create(WindowRect nativeButton, Vector2 viewport, float buttonScale, float fontSize, float uiScale, Vector2 offset = default)
    {
        var font = fontSize * uiScale;
        var height = nativeButton.Height * buttonScale;
        return new(new(viewport.X + nativeButton.X + nativeButton.Width + 8 * uiScale + offset.X,
            viewport.Y + nativeButton.Y + (nativeButton.Height - height) / 2 + offset.Y),
            height, font, 4 * uiScale);
    }
}
