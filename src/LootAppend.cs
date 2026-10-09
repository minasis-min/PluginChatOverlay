namespace PluginChatOverlay;

internal static class LootAppend
{
    // ItemLoot (62): split only a proven appended byte suffix, never the vanilla acquisition text.
    public static bool Split(int kind, ReadOnlySpan<byte> original, ReadOnlySpan<byte> modified, out byte[] addition)
    {
        addition = [];
        if (kind != 62 || original.IsEmpty || modified.Length <= original.Length || !modified.StartsWith(original)) return false;
        if (modified.Length > History.MaxRecordBytes) return false;
        addition = modified[original.Length..].ToArray();
        return true;
    }
}
