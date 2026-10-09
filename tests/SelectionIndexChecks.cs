using System.Diagnostics;
using PluginChatOverlay;

internal static class SelectionIndexChecks
{
    public static void Run()
    {
        var ids = Enumerable.Range(1, History.MaxRecords).Select(i => (long)i).ToArray();
        var indices = ids.Select((id, index) => (id, index)).ToDictionary(p => p.id, p => p.index);
        var selection = new TextSelection { Start = new(100, 2), End = new(900, 5), Dragged = true };
        for (var direction = 0; direction < 2; direction++)
        {
            foreach (var id in ids)
            {
                var linear = selection.Range(ids, id, 20, out var from, out var to);
                var indexed = selection.Range(indices, id, 20, out var indexedFrom, out var indexedTo);
                if (linear != indexed || from != indexedFrom || to != indexedTo) throw new Exception("selection index mismatch");
            }
            (selection.Start, selection.End) = (selection.End, selection.Start);
        }
        Console.WriteLine("PASS indexed selection matches linear ranges across 1000 rows in both directions");
        var watch = Stopwatch.StartNew();
        for (var pass = 0; pass < 10; pass++) foreach (var id in ids) selection.Range(ids, id, 20, out _, out _);
        var linearMs = watch.Elapsed.TotalMilliseconds; watch.Restart();
        for (var pass = 0; pass < 10; pass++) foreach (var id in ids) selection.Range(indices, id, 20, out _, out _);
        var indexedMs = watch.Elapsed.TotalMilliseconds;
        Console.WriteLine($"Selection range microbenchmark: linear {linearMs:F2} ms, indexed {indexedMs:F2} ms (10000 queries, synthetic, not game FPS)");
        selection.Start = new(2000, 0);
        if (selection.Range(indices, 100, 20, out _, out _)) throw new Exception("evicted selection was redirected");
        Console.WriteLine("PASS indexed selection rejects missing endpoints rather than redirecting selection");
    }
}
