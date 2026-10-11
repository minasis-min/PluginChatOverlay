using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Lumina.Text.Payloads;
using Lumina.Text.ReadOnly;

namespace PluginChatOverlay;

internal sealed class ChatPalette(IDataManager data, IPluginLog log)
{
    private readonly Dictionary<int, byte[]> prefixes = [];
    public HashSet<int> MissingKinds { get; } = [];
    public byte[] Prefix(int kind)
    {
        if (prefixes.TryGetValue(kind, out var cached)) return cached;
        try
        {
            var format = data.GetExcelSheet<LogKind>().GetRow((uint)kind).Format.AsSpan();
            var prefix = FirstColor(format.Data);
            if (prefix.Length == 0) MissingKinds.Add(kind);
            return prefixes[kind] = prefix;
        }
        catch (Exception ex) { MissingKinds.Add(kind); log.Warning(ex, "Cannot read vanilla chat color for {Kind}", kind); return prefixes[kind] = []; }
    }
    internal static byte[] FirstColor(ReadOnlySpan<byte> format)
    {
        foreach (var payload in new ReadOnlySeStringSpan(format))
        {
            if (payload.Type != ReadOnlySePayloadType.Macro) continue;
            if (payload.MacroCode is not (MacroCode.Color or MacroCode.ColorType)) continue;
            var bytes = new byte[payload.EnvelopeByteLength]; payload.WriteEnvelopeTo(bytes); return bytes;
        }
        return [];
    }

}

internal sealed class DisplayDocument(ChatRecord record, string plugin, Settings settings, ChatPalette palette, int revision)
{
    public ChatRecord Record { get; } = record;
    public string Plugin { get; } = plugin;
    public int Revision { get; } = revision;
    public byte[] Bytes { get; } = Content(record, settings, palette.Prefix(record.Kind));
    public string Text { get; private set; } = "";
    public void CacheText() => Text = SeString.Parse(Bytes).TextValue;
    internal static byte[] Content(ChatRecord record, Settings settings, byte[] bodyColor)
    {
        var builder = new SeStringBuilder();
        if (settings.ShowTime)
        {
            var rgba = settings.TimestampColor | 0xFF000000;
            var bgra = (rgba & 0xFF00FF00) | ((rgba & 0xFF) << 16) | ((rgba >> 16) & 0xFF);
            var timestamp = ReadOnlySeString.FromMacroString($"<color({bgra})>{ChatClock.Format(record, settings.PadTimestampHour)}<color(stackcolor)> ");
            builder.Append(SeString.Parse(timestamp.AsSpan().Data.ToArray()));
        }
        builder.Append(SeString.Parse(bodyColor));
        if (record.Sender.Length > 0) builder.Append(SeString.Parse(record.Sender)).AddText(": ");
        builder.Append(SeString.Parse(record.Body));
        return builder.Build().Encode();
    }
}
