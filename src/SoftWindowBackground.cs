using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace PluginChatOverlay;

internal static class SoftWindowBackground
{
    private const int Layers = 64;

    // Integrated Gaussian edge profile, normalized over +/- three standard deviations.
    internal static float Coverage(float inwardDistance, float sigma)
    {
        var x = inwardDistance / Math.Max(.01f, sigma);
        if (x <= -3) return 0;
        if (x >= 3) return 1;
        const float tail = .0013499f;
        return Math.Clamp((GaussianCdf(x) - tail) / (1 - 2 * tail), 0, 1);
    }

    private static float GaussianCdf(float x)
    {
        var absolute = MathF.Abs(x);
        var t = 1 / (1 + .2316419f * absolute);
        var polynomial = t * (.319381530f + t * (-.356563782f + t * (1.781477937f
            + t * (-1.821255978f + t * 1.330274429f))));
        var tail = .3989422804f * MathF.Exp(-absolute * absolute / 2) * polynomial;
        return x >= 0 ? 1 - tail : tail;
    }

    internal static void Draw(ImDrawListPtr draw, Vector2 start, Vector2 end, uint color, float opacity, float width, float rounding)
    {
        if (opacity <= 0 || end.X <= start.X || end.Y <= start.Y) return;
        var radius = Math.Min(Math.Max(.1f, width) * 3, Math.Min(end.X - start.X, end.Y - start.Y) / 2);
        var sigma = radius / 3;
        var accumulated = 0f;
        for (var i = 0; i < Layers; i++)
        {
            var distance = -radius + 2 * radius * i / (Layers - 1);
            var target = Math.Clamp(opacity, 0, 1) * Coverage(distance, sigma);
            var alpha = (target - accumulated) / Math.Max(.0001f, 1 - accumulated);
            var alphaByte = (uint)Math.Clamp((int)MathF.Round(alpha * 255), 0, 255);
            if (alphaByte == 0) continue;
            accumulated += (1 - accumulated) * alphaByte / 255f;
            var inset = new Vector2(distance);
            draw.AddRectFilled(start + inset, end - inset, (color & 0x00FFFFFFu) | (alphaByte << 24),
                Math.Max(0, rounding - distance));
        }
    }

    internal static void DrawBorder(ImDrawListPtr draw, Vector2 start, Vector2 end, uint color, float thickness, float fadeWidth, float rounding)
    {
        if (thickness <= 0 || (color >> 24) == 0 || end.X <= start.X || end.Y <= start.Y) return;
        var sigma = Math.Max(.1f, fadeWidth);
        var radius = sigma * 3;
        var limit = Math.Min(end.X - start.X, end.Y - start.Y) / 2;
        var inner = Math.Min(thickness + radius, limit);
        var step = (inner + radius) / Layers;
        for (var i = 0; i < Layers; i++)
        {
            var distance = -radius + step * (i + .5f);
            var coverage = Coverage(distance, sigma) - Coverage(distance - thickness, sigma);
            var alpha = (uint)Math.Clamp((int)MathF.Round((color >> 24) * coverage), 0, 255);
            if (alpha == 0) continue;
            var inset = new Vector2(distance);
            draw.AddRect(start + inset, end - inset, (color & 0x00FFFFFFu) | (alpha << 24),
                Math.Max(0, rounding - distance), ImDrawFlags.None, step);
        }
    }
}
