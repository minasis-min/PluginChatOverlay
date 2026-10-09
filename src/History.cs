namespace PluginChatOverlay;

internal sealed record ChatRecord(long Id, int Kind, int Source, int Target, int Timestamp,
    byte[] Sender, byte[] Body, string Plain)
{
    public int Bytes => Sender.Length + Body.Length;
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.Now;
}

/// <summary>No unbounded handoff queue: owned immutable records go directly into bounded history.</summary>
internal sealed class History
{
    public const int MaxRecordBytes = 64 * 1024;
    public const int MaxRecords = 1000;
    public const int MaxHistoryBytes = 16 * 1024 * 1024;
    private readonly object gate = new();
    private readonly List<ChatRecord> records = [];
    private int bytes;
    private long sequence;
    private ChatRecord[]? cachedSnapshot;
    private long removalRevision;
    public long RemovalRevision { get { lock (gate) return removalRevision; } }
    public int Limit { get; set; } = 1000;
    public long Evictions { get; private set; }

    public bool TryAdd(Func<long, ChatRecord> factory, out long id)
    {
        lock (gate)
        {
            id = 0;
            var entry = factory(sequence + 1); // Copy/parse must finish before changing the history or suppressing.
            if (entry.Bytes > MaxRecordBytes) return false;
            cachedSnapshot = null;
            records.Add(entry); bytes += entry.Bytes; id = ++sequence;
            Trim(); return true;
        }
    }
    public void Replace(long id, ChatRecord replacement)
    {
        lock (gate)
        {
            var index = records.FindIndex(r => r.Id == id);
            if (index < 0 || replacement.Bytes > MaxRecordBytes) return;
            cachedSnapshot = null;
            bytes += replacement.Bytes - records[index].Bytes;
            records[index] = replacement with { ReceivedAt = records[index].ReceivedAt }; Trim();
        }
    }
    public void Remove(long id)
    {
        lock (gate)
        {
            var index = records.FindIndex(r => r.Id == id);
            if (index < 0) return;
            cachedSnapshot = null;
            bytes -= records[index].Bytes; records.RemoveAt(index);
            removalRevision++;
        }
    }
    public ChatRecord[] Snapshot() { lock (gate) return records.ToArray(); }
    // Shared with the renderer only; callers must not mutate this owned snapshot.
    public ChatRecord[] RenderSnapshot() { lock (gate) return cachedSnapshot ??= records.ToArray(); }
    public void Clear() { lock (gate) { records.Clear(); bytes = 0; cachedSnapshot = null; removalRevision++; } }
    public void ApplyLimit(int limit) { lock (gate) { Limit = Math.Clamp(limit, 10, MaxRecords); Trim(); } }
    private void Trim()
    {
        var remove = 0; var limit = Math.Clamp(Limit, 10, MaxRecords);
        while (records.Count - remove > limit || bytes > MaxHistoryBytes)
        {
            bytes -= records[remove++].Bytes; Evictions++;
        }
        if (remove == 0) return;
        records.RemoveRange(0, remove); cachedSnapshot = null; removalRevision++;
    }
}
