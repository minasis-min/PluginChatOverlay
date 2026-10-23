namespace PluginChatOverlay;

internal enum ChatRecordOrigin { Plugin, Game }

internal sealed record ChatRecord(long Id, int Kind, int Source, int Target, int Timestamp,
    byte[] Sender, byte[] Body, string Plain)
{
    public int Bytes => Sender.Length + Body.Length;
    public ChatRecordOrigin Origin { get; init; }
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.Now;
}

/// <summary>One sequence for display, independent bounded buckets so battle traffic cannot evict plugin notifications.</summary>
internal sealed class History
{
    public const int MaxRecordBytes = 64 * 1024;
    public const int MaxRecords = 1000;
    public const int DefaultRecords = 100;
    public const int MaxHistoryBytes = 16 * 1024 * 1024; // Per origin.
    private readonly object gate = new();
    private readonly LinkedList<Entry> records = new();
    private readonly Dictionary<long, Entry> index = [];
    private readonly Bucket plugins = new(), game = new();
    private long sequence;
    private ChatRecord[]? cachedSnapshot;
    private long removalRevision;
    public long RemovalRevision { get { lock (gate) return removalRevision; } }
    public int Limit { get; set; } = DefaultRecords;
    public int GameLimit { get; private set; } = DefaultRecords;
    public long Evictions { get; private set; }
    private sealed class Bucket
    {
        public readonly LinkedList<Entry> Records = new();
        public int Bytes;
    }
    private sealed class Entry(ChatRecord record, Bucket bucket)
    {
        public readonly ChatRecord Record = record;
        public readonly Bucket Bucket = bucket;
        public LinkedListNode<Entry> Ordered = null!, InBucket = null!;
    }
    public bool TryAdd(Func<long, ChatRecord> factory, out long id)
    {
        lock (gate)
        {
            id = 0;
            var record = factory(sequence + 1); // Copy/parse finishes before ownership or suppression.
            if (record.Bytes > MaxRecordBytes || record.Origin is not (ChatRecordOrigin.Plugin or ChatRecordOrigin.Game)) return false;
            var bucket = record.Origin == ChatRecordOrigin.Game ? game : plugins;
            var entry = new Entry(record, bucket);
            entry.Ordered = records.AddLast(entry); entry.InBucket = bucket.Records.AddLast(entry);
            bucket.Bytes += record.Bytes; index.Add(record.Id, entry);
            id = ++sequence; cachedSnapshot = null; Trim(); return true;
        }
    }
    public void Remove(long id)
    {
        lock (gate) if (index.TryGetValue(id, out var entry)) Remove(entry);
    }
    private void Remove(Entry entry)
    {
        entry.Bucket.Bytes -= entry.Record.Bytes; entry.Bucket.Records.Remove(entry.InBucket);
        records.Remove(entry.Ordered); index.Remove(entry.Record.Id);
        cachedSnapshot = null; removalRevision++;
    }
    public ChatRecord[] Snapshot() { lock (gate) return records.Select(e => e.Record).ToArray(); }
    // Shared with the renderer only; callers must not mutate this owned snapshot.
    public ChatRecord[] RenderSnapshot() { lock (gate) return cachedSnapshot ??= records.Select(e => e.Record).ToArray(); }
    public void Clear()
    {
        lock (gate)
        {
            records.Clear(); index.Clear(); plugins.Records.Clear(); game.Records.Clear();
            plugins.Bytes = game.Bytes = 0; cachedSnapshot = null; removalRevision++;
        }
    }
    public void ApplyLimit(int limit) { lock (gate) { Limit = Math.Clamp(limit, 10, MaxRecords); Trim(); } }
    public void ApplyGameLimit(int limit) { lock (gate) { GameLimit = Math.Clamp(limit, 10, MaxRecords); Trim(); } }
    private void Trim()
    {
        Trim(plugins, Limit); Trim(game, GameLimit);
    }
    private void Trim(Bucket bucket, int limit)
    {
        while (bucket.Records.Count > Math.Clamp(limit, 10, MaxRecords) || bucket.Bytes > MaxHistoryBytes)
        { Remove(bucket.Records.First!.Value); Evictions++; }
    }
}
