using System.Globalization;
using System.Numerics;

namespace PluginChatOverlay;

internal static class WindowTitle
{
    public static string MainTitle(bool show) => (show ? "Plugin Chat Overlay" : "") + "###PluginChatOverlay.Chat";
    public static string Detached(string name, string id, int unread) => name + (unread > 0 ? $" ({unread})" : "") + "###PluginChatOverlay.Detached." + id;
}

internal sealed class PluginTags
{
    private readonly Dictionary<string, HashSet<string>> owners = new(StringComparer.Ordinal);
    public void Reset(IEnumerable<(string Id, string Name)> plugins)
    {
        owners.Clear();
        foreach (var (id, name) in plugins) { Add(id, id); Add(name, id); }
    }
    private void Add(string tag, string plugin)
    {
        if (string.IsNullOrWhiteSpace(tag) || string.IsNullOrWhiteSpace(plugin)) return;
        if (!owners.TryGetValue(tag, out var set)) owners[tag] = set = [];
        set.Add(plugin);
    }
    public string Resolve(string text)
    {
        var token = Token(text);
        return owners.TryGetValue(token, out var set) && set.Count == 1 ? set.Single() : "";
    }
    internal static string Token(string text)
    {
        text = text.TrimStart();
        if (text.StartsWith('['))
        {
            var end = text.IndexOf(']');
            return end > 1 ? text[1..end] : "";
        }
        var colon = text.IndexOf(':');
        return colon > 0 && colon <= 100 ? text[..colon].TrimEnd() : "";
    }
}

internal readonly record struct WindowRect(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;
    public float Bottom => Y + Height;
}
internal static class WindowSnap
{
    public static Vector2 Position(WindowRect moving, WindowRect screen, IEnumerable<WindowRect> windows, float distance)
    {
        var dx = distance + 1; var dy = distance + 1;
        void X(float shift) { if (Math.Abs(shift) <= distance && Math.Abs(shift) < Math.Abs(dx)) dx = shift; }
        void Y(float shift) { if (Math.Abs(shift) <= distance && Math.Abs(shift) < Math.Abs(dy)) dy = shift; }
        X(screen.X - moving.X); X(screen.Right - moving.Right);
        Y(screen.Y - moving.Y); Y(screen.Bottom - moving.Bottom);
        foreach (var other in windows)
        {
            if (moving.Y <= other.Bottom + distance && moving.Bottom >= other.Y - distance)
            { X(other.X - moving.X); X(other.Right - moving.Right); X(other.Right - moving.X); X(other.X - moving.Right); }
            if (moving.X <= other.Right + distance && moving.Right >= other.X - distance)
            { Y(other.Y - moving.Y); Y(other.Bottom - moving.Bottom); Y(other.Bottom - moving.Y); Y(other.Y - moving.Bottom); }
        }
        return new(moving.X + (Math.Abs(dx) <= distance ? dx : 0), moving.Y + (Math.Abs(dy) <= distance ? dy : 0));
    }
}

internal readonly record struct SelectionPoint(long RecordId, int Character);
internal sealed class TextSelection
{
    public SelectionPoint? Start, End;
    public bool Active, Dragged;
    public Vector2 PressPosition = Vector2.Zero;
    public void Clear() { Start = End = null; Active = Dragged = false; }
    public void EndDrag() => Active = false;
    public static int Boundary(string text, int index)
    {
        index = Math.Clamp(index, 0, text.Length);
        if (index == text.Length) return index;
        var boundaries = StringInfo.ParseCombiningCharacters(text);
        return boundaries.LastOrDefault(p => p <= index);
    }
    public bool Range(IReadOnlyList<long> ids, long id, int textLength, out int from, out int to)
    {
        from = to = 0;
        if (!Dragged || Start == null || End == null) return false;
        var a = IndexOf(ids, Start.Value.RecordId); var b = IndexOf(ids, End.Value.RecordId); var i = IndexOf(ids, id);
        return RangeAt(a, b, i, textLength, out from, out to);
    }
    public bool Range(IReadOnlyDictionary<long, int> indices, long id, int textLength, out int from, out int to)
    {
        from = to = 0;
        if (!Dragged || Start == null || End == null) return false;
        var a = indices.TryGetValue(Start.Value.RecordId, out var startIndex) ? startIndex : -1;
        var b = indices.TryGetValue(End.Value.RecordId, out var endIndex) ? endIndex : -1;
        var i = indices.TryGetValue(id, out var rowIndex) ? rowIndex : -1;
        return RangeAt(a, b, i, textLength, out from, out to);
    }
    private bool RangeAt(int a, int b, int i, int textLength, out int from, out int to)
    {
        from = to = 0;
        if (a < 0 || b < 0 || i < 0) return false;
        var start = Start!.Value; var end = End!.Value;
        if (a > b || (a == b && start.Character > end.Character)) { (a, b) = (b, a); (start, end) = (end, start); }
        if (i < a || i > b) return false;
        from = i == a ? Math.Clamp(start.Character, 0, textLength) : 0;
        to = i == b ? Math.Clamp(end.Character, 0, textLength) : textLength;
        return to > from;
    }
    public string Extract(IReadOnlyList<(long Id, string Text)> rows)
    {
        if (!Dragged || Start == null || End == null) return "";
        var ids = rows.Select(r => r.Id).ToArray(); var parts = new List<string>();
        var a = IndexOf(ids, Start.Value.RecordId); var b = IndexOf(ids, End.Value.RecordId);
        var start = Start.Value; var end = End.Value;
        if (a < 0 || b < 0) return "";
        if (a > b || (a == b && start.Character > end.Character)) { (a, b) = (b, a); (start, end) = (end, start); }
        for (var i = a; i <= b; i++)
        {
            var row = rows[i];
            var from = i == a ? Boundary(row.Text, start.Character) : 0;
            var to = i == b ? Boundary(row.Text, end.Character) : row.Text.Length;
            parts.Add(row.Text[from..to]);
        }
        return string.Join(Environment.NewLine, parts);
    }
    private static int IndexOf(IReadOnlyList<long> ids, long id) { for (var i = 0; i < ids.Count; i++) if (ids[i] == id) return i; return -1; }
}

internal static class ChatClock
{
    public static string Format(ChatRecord record) => (record.Timestamp > 0 ? DateTimeOffset.FromUnixTimeSeconds(record.Timestamp) : record.ReceivedAt).ToLocalTime().ToString("[HH:mm]", CultureInfo.InvariantCulture);
}

internal static class ChatRowGeometry
{
    public static float Advance(float renderedHeight, float lineHeight, float itemSpacing)
        => Math.Max(lineHeight, renderedHeight) + itemSpacing;
}
