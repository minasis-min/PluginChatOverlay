using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Plugin.Services;
using PluginChatOverlay;

internal static class RenderCacheChecks
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    public static void Run()
    {
        var plugin = (Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
        var history = new History(); history.ApplyLimit(20);
        var data = DispatchProxy.Create<IDataManager, ServiceProxy>();
        var log = DispatchProxy.Create<IPluginLog, ServiceProxy>();
        var palette = new ChatPalette(data, log);
        var cachedDocs = new Dictionary<long, DisplayDocument>();
        for (var i = 0; i < 20; i++)
        {
            history.TryAdd(id => new(id, 0, 0, 0, 0, [], [(byte)'a'], "a"), out _);
        }
        var snapshot = history.Snapshot();
        var docs = snapshot.Select(r => new DisplayDocument(r, "", new Settings(), palette, 0)).ToArray();
        foreach (var doc in docs) cachedDocs.Add(doc.Record.Id, doc);
        Set(plugin, "history", history); Set(plugin, "documents", cachedDocs); Set(plugin, "renderCacheGate", new object());
        Set(plugin, "prunedRemovalRevision", -1L);
        var views = (IDictionary)Activator.CreateInstance(typeof(Plugin).GetField("views", Fields)!.FieldType)!;
        var viewType = typeof(Plugin).GetNestedType("ViewState", BindingFlags.NonPublic)!;
        var rowType = typeof(Plugin).GetNestedType("RowLayout", BindingFlags.NonPublic)!;
        var state = Activator.CreateInstance(viewType, true)!; views.Add("hidden", state); Set(plugin, "views", views);
        var rows = (IDictionary)viewType.GetField("Rows", Fields)!.GetValue(state)!;
        foreach (var doc in docs) rows.Add(doc.Record.Id, Activator.CreateInstance(rowType, [doc])!);
        Set(state, "Documents", docs); Set(state, "FilterSource", docs); Set(state, "FilteredDocuments", docs);
        Set(state, "UnreadDocuments", docs);
        Set(plugin, "renderedSnapshot", snapshot); Set(plugin, "renderedDocuments", docs); Set(plugin, "frameDocuments", docs);
        var prune = typeof(Plugin).GetMethod("PruneHistoryCaches", Fields)!;
        history.TryAdd(id => new(id, 0, 0, 0, 0, [], [(byte)'b'], "b"), out _);
        prune.Invoke(plugin, null);
        Check(!cachedDocs.ContainsKey(snapshot[0].Id) && !rows.Contains(snapshot[0].Id) && rows.Count == 19
            && ((Array)Get(state, "Documents")!).Length == 0 && Get(state, "FilterSource") == null,
            "automatic eviction releases hidden view documents and rows without drawing that tab");
        history.ApplyLimit(10); prune.Invoke(plugin, null);
        Check(rows.Count == 9 && cachedDocs.Count == 9 && !rows.Contains(snapshot[1].Id),
            "history limit reduction purges evicted hidden-view rows immediately");
        history.Clear(); prune.Invoke(plugin, null);
        Check(cachedDocs.Count == 0 && rows.Count == 0 && Get(plugin, "renderedSnapshot") == null
            && ((Array)Get(plugin, "renderedDocuments")!).Length == 0 && ((Array)Get(plugin, "frameDocuments")!).Length == 0
            && ((Array)Get(state, "FilteredDocuments")!).Length == 0 && Get(state, "UnreadDocuments") == null,
            "history clear releases shared and hidden-view caches without drawing any windows");
    }
    private static object? Get(object target, string name) => target.GetType().GetField(name, Fields)!.GetValue(target);
    private static void Set(object target, string name, object? value) => target.GetType().GetField(name, Fields)!.SetValue(target, value);
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
}
