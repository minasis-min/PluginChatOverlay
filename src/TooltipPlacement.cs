using System.Numerics;

namespace PluginChatOverlay;

internal static class TooltipPlacement
{
    private static bool Overlaps(WindowRect a, WindowRect b) => a.X < b.X + b.Width && a.X + a.Width > b.X
        && a.Y < b.Y + b.Height && a.Y + a.Height > b.Y;
    public static Vector2? Position(WindowRect tooltip, WindowRect[] chats, Vector2 screen)
    {
        if (!chats.Any(r => Overlaps(tooltip, r)) || screen.X < tooltip.Width || screen.Y < tooltip.Height) return null;
        Vector2? best = null; var distance = float.MaxValue;
        foreach (var chat in chats)
        foreach (var candidate in new Vector2[] {
            new(chat.X - tooltip.Width - 5, tooltip.Y), new(chat.X + chat.Width + 5, tooltip.Y),
            new(tooltip.X, chat.Y - tooltip.Height - 5), new(tooltip.X, chat.Y + chat.Height + 5) })
        {
            var pos = new Vector2(Math.Clamp(candidate.X, 0, screen.X - tooltip.Width), Math.Clamp(candidate.Y, 0, screen.Y - tooltip.Height));
            if (chats.Any(r => Overlaps(new(pos.X, pos.Y, tooltip.Width, tooltip.Height), r))) continue;
            var delta = Vector2.DistanceSquared(pos, new(tooltip.X, tooltip.Y));
            if (delta < distance) { best = pos; distance = delta; }
        }
        return best;
    }
}
