using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
namespace PluginChatOverlay;

// Only a single, payload-aligned ALI insertion may be removed. The original
// game's byte stream is the replacement; display text is never reconstructed.
internal static class TeleportRoute
{
    private static readonly byte[] Terminator = RawPayload.LinkTerminator.Encode();
    private static readonly byte[][] Decorations = [new UIForegroundPayload(69).Encode(), UIForegroundPayload.UIForegroundOff.Encode(), new UIForegroundPayload(500).Encode(), new UIGlowPayload(501).Encode(), UIGlowPayload.UIGlowOff.Encode()];
    private static readonly byte[][] Icons = new[] { BitmapFontIcon.Aetheryte, BitmapFontIcon.Aethernet, BitmapFontIcon.FlyZone, BitmapFontIcon.CrossWorld }.Select(i => new IconPayload(i).Encode()).ToArray();
    internal static bool OwnsLink(IChatGui chat, DalamudLinkPayload link) => chat.RegisteredLinkHandlers.TryGetValue((link.Plugin,link.CommandId),out var handler)
        && handler.Method.DeclaringType?.FullName == "Divination.AetheryteLinkInChat.AetheryteLinkInChat";
    internal static bool TryRestore(int kind, byte[] original, byte[] modified, Func<DalamudLinkPayload, bool> registered, out SeString restored)
        => TryRestore(kind,original,modified,registered,out restored,out _);
    internal static bool TryRestore(int kind, byte[] original, byte[] modified, Func<DalamudLinkPayload, bool> registered, out SeString restored, out byte[] addition)
    {
        restored = null!; addition = [];
        if (!PositionMessages.IsPlayerKind(kind) || original.Length == 0 || modified.Length <= original.Length || modified.Length > History.MaxRecordBytes) return false;
        try
        {
            var parsed = SeString.Parse(original);
            if (!parsed.Payloads.Any(p => p is MapLinkPayload) || !parsed.Encode().AsSpan().SequenceEqual(original)) return false;
            var prefix = 0; while (prefix < original.Length && original[prefix] == modified[prefix]) prefix++;
            var suffix = 0; while (suffix < original.Length && original[^(suffix + 1)] == modified[^(suffix + 1)]) suffix++;
            if (prefix + suffix < original.Length) return false;
            var position = 0;
            foreach (var payload in parsed.Payloads.Append<Payload>(null!))
            {
                if (position >= original.Length - suffix && position <= prefix)
                {
                    var added = modified.AsSpan(position, modified.Length - original.Length).ToArray();
                    if (OwnedRoute(added, registered)) { restored = parsed; addition = added; return true; }
                }
                if (payload != null) position += payload.Encode().Length;
            }
        }
        catch { /* Unknown/malformed formats remain native. */ }
        return false;
    }
    internal static byte[] MapContext(byte[] original)
    {
        try
        {
            var parts=SeString.Parse(original).Payloads;
            var start=parts.FindIndex(p=>p is MapLinkPayload);
            if(start<0) return [];
            var result=new SeString(new TextPayload("\uE0BB"));
            for(var i=start;i<parts.Count;i++)
            {
                result.Payloads.Add(parts[i]);
                if(parts[i].Encode().AsSpan().SequenceEqual(Terminator)) return result.Encode();
            }
        }
        catch { }
        return [];
    }
    private static bool OwnedRoute(byte[] bytes, Func<DalamudLinkPayload, bool> registered)
    {
        var parts = SeString.Parse(bytes).Payloads;
        if (parts.Count < 8 || parts[^1] is not TextPayload { Text: "]" }) return false;
        var first = parts[0] is TextPayload { Text: "\n" } ? 1 : 0;
        if (parts[first] is not IconPayload || !parts[first].Encode().AsSpan().SequenceEqual(Icons[0])) return false;
        string? owner = null; uint? current = null; var raw = false; var sawAetheryte = false; var sawLifestream = false;
        for (var i = first; i < parts.Count; i++)
        {
            var part = parts[i]; var encoded = part.Encode();
            if (part is DalamudLinkPayload link)
            {
                if (current != null || sawLifestream || link.CommandId > 1 || !registered(link) || owner != null && owner != link.Plugin) return false;
                owner = link.Plugin; current = link.CommandId; raw = false;
                if (current == 1 && (i + 1 >= parts.Count || parts[i + 1] is not TextPayload { Text: "Lifestream" })) return false;
            }
            else if (encoded.AsSpan().SequenceEqual(Terminator))
            {
                if (current == null || !raw) return false;
                if (current == 0) sawAetheryte = true; else sawLifestream = true;
                current = null;
            }
            else if (part is TextPayload text)
            {
                if (text.Text == null || text.Text.Contains('\n') || text.Text.Contains('\r')) return false;
                // Once the last link closes only its color reset and closing bracket remain.
                if (sawLifestream && i != parts.Count - 1) return false;
            }
            else if (part is IconPayload)
            {
                if (current != null || sawLifestream || !Icons.Any(v => encoded.AsSpan().SequenceEqual(v))) return false;
            }
            else if (Decorations.Any(v => encoded.AsSpan().SequenceEqual(v))) { }
            else if (part is RawPayload && current != null && !raw) raw = true;
            else return false;
        }
        return current == null && sawAetheryte && sawLifestream;
    }
}
