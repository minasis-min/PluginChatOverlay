namespace PluginChatOverlay;

// Rebuild only with the tab's filtered snapshot. Deduplicate against an actually
// visible original statement, so another tab's filters or history eviction cannot
// remove this tab's coordinate context.
internal static class RoutePresentation
{
    internal static DisplayDocument[] Arrange(DisplayDocument[] documents,Settings settings,ChatPalette palette)
    {
        var routes=documents.Where(d=>d.Record.MapContext.Length>0).Select(d=>d.Record.Id).ToHashSet();
        if(routes.Count==0) return documents;
        var originals=new Dictionary<long,DisplayDocument>();
        foreach(var doc in documents)
            if(doc.Record.Origin==ChatRecordOrigin.Game && routes.Contains(doc.Record.RelatedRouteId)) originals.TryAdd(doc.Record.RelatedRouteId,doc);
        var moved=originals.Values.Select(d=>d.Record.Id).ToHashSet();
        var result=new List<DisplayDocument>(documents.Length);
        foreach(var doc in documents)
        {
            if(moved.Contains(doc.Record.Id)) continue;
            if(!routes.Contains(doc.Record.Id)) { result.Add(doc); continue; }
            if(originals.TryGetValue(doc.Record.Id,out var original)) { result.Add(original); result.Add(doc); }
            else result.Add(doc.WithRouteMap(settings,palette));
        }
        return result.ToArray();
    }
}
