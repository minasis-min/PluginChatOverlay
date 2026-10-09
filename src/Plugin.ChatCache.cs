using Dalamud.Game.Text.SeStringHandling;

namespace PluginChatOverlay;

public sealed partial class Plugin
{
    private ChatRecord[]? renderedSnapshot;
    private int renderedRevision = -1;
    private DisplayDocument[] renderedDocuments = [];
    private readonly object renderCacheGate = new();
    private long prunedRemovalRevision = -1;
    private void PruneHistoryCaches()
    {
        lock (renderCacheGate)
        {
            var revision = history.RemovalRevision;
            if (revision == prunedRemovalRevision) return;
            var live = history.Snapshot().Select(r => r.Id).ToHashSet();
            foreach (var id in documents.Keys.Where(id => !live.Contains(id)).ToArray()) documents.Remove(id);
            renderedSnapshot = null; renderedDocuments = []; frameDocuments = [];
            foreach (var state in views.Values)
            {
                state.Documents = []; state.DocumentIds = []; state.ActiveIds = [];
                state.DocumentIndices.Clear();
                state.FilterSource = null; state.FilteredDocuments = []; state.UnreadDocuments = null;
                foreach (var id in state.Rows.Keys.Where(id => !live.Contains(id)).ToArray()) state.Rows.Remove(id);
                if (state.Selection.Start is { } start && !live.Contains(start.RecordId)
                    || state.Selection.End is { } end && !live.Contains(end.RecordId)) state.Selection.Clear();
            }
            prunedRemovalRevision = revision;
        }
    }
    private DisplayDocument Document(ChatRecord record)
    {
        if (documents.TryGetValue(record.Id, out var cached) && ReferenceEquals(cached.Record, record) && cached.Revision == presentationRevision) return cached;
        var plugin = tags.Resolve(SeString.Parse(record.Body).TextValue);
        if (plugin.Length > 0) recognised.Add(plugin);
        var doc = new DisplayDocument(record, plugin, settings, palette, presentationRevision);
        doc.CacheText(); documents[record.Id] = doc; return doc;
    }
    private ViewState View(string id)
    {
        if (!views.TryGetValue(id, out var state)) views[id] = state = new();
        return state;
    }
    private void InvalidateRowLayouts()
    {
        foreach (var view in views.Values) view.Rows.Clear();
    }
}
