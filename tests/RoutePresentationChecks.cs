using System.Reflection;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using PluginChatOverlay;
internal static class RoutePresentationChecks
{
    private static void Check(bool value,string label) { if(!value) throw new Exception(label); Console.WriteLine("PASS "+label); }
    internal static void Run()
    {
        var settings=new Settings { ShowTime=false };
        var palette=new ChatPalette(DispatchProxy.Create<IDataManager,ServiceProxy>(),DispatchProxy.Create<IPluginLog,ServiceProxy>());
        var original=new SeString(new TextPayload("tesuto "),new MapLinkPayload(1,1,100,200),new TextPayload("中央ラノシア (17.3, 9.2)"),RawPayload.LinkTerminator,new TextPayload(" test")).Encode();
        var map=TeleportRoute.MapContext(original);
        var mapText=SeString.Parse(map).TextValue;
        Check(PositionMessages.HasMapLink(map) && mapText.Contains("中央ラノシア") && !mapText.Contains("tesuto") && !mapText.Contains("test"),
            "route coordinate context copies only the complete native map link, not surrounding player text");
        var record=new ChatRecord(1,10,0,0,0,[],new SeString(new TextPayload("移動案内 Lifestream")).Encode(),"route") { PluginOwner="AetheryteLinkInChat", MapContext=map };
        var route=new DisplayDocument(record,record.PluginOwner,settings,palette,0); route.CacheText();
        var sourceRecord=new ChatRecord(2,10,0,0,0,[],original,"speech") { Origin=ChatRecordOrigin.Game, RelatedRouteId=1 };
        var speech=new DisplayDocument(sourceRecord,"",settings,palette,0); speech.CacheText();
        var onlyRoute=RoutePresentation.Arrange([route],settings,palette);
        Check(onlyRoute.Length==1 && onlyRoute[0].Text.Contains("中央ラノシア") && onlyRoute[0].Text.Contains("Lifestream") && !onlyRoute[0].Text.Contains("tesuto"),
            "route-only tabs show coordinates and route together without copying the player statement");
        Check(ReferenceEquals(onlyRoute[0],RoutePresentation.Arrange([route],settings,palette)[0]),"coordinate route variants are cached instead of rebuilt every frame");
        var pair=RoutePresentation.Arrange([route,speech],settings,palette);
        Check(pair.Select(d=>d.Record.Id).SequenceEqual([2L,1L]) && pair[0].Text.Contains("tesuto") && !pair[1].Text.Contains("中央ラノシア"),
            "visible original speech moves before its own route and coordinates occur only in the original");
        Check(ReferenceEquals(pair[1],route),"tabs with visible map statements retain the route-only document");
        Check(onlyRoute[0].Text.Contains("中央ラノシア") && !pair[1].Text.Contains("中央ラノシア"),"two tabs can independently show coordinate context without changing the shared route record");
        var unrelated=new DisplayDocument(sourceRecord with { Id=3, RelatedRouteId=0 },"",settings,palette,0); unrelated.CacheText();
        var sameCoordinates=RoutePresentation.Arrange([route,unrelated],settings,palette);
        Check(sameCoordinates[0].Text.Contains("中央ラノシア") && sameCoordinates[1].Record.Id==3,"equal coordinates in unrelated messages do not cause false deduplication or reordering");
        Check(RoutePresentation.Arrange([route],settings,palette)[0].Text.Contains("中央ラノシア"),"filtering or evicting the original restores route coordinate context");
        var selection=new TextSelection { Dragged=true, Start=new(2,0), End=new(1,route.Text.Length) };
        Check(selection.Extract(pair.Select(d=>(d.Record.Id,d.Text)).ToArray())==speech.Text+Environment.NewLine+route.Text,
            "text selection follows displayed original-before-route order even when capture IDs are reversed");
        var history=new History(); history.TryAdd(id=>record with { Id=id },out var routeId);
        var restored=SeString.Parse(original); history.RememberRoute(restored,routeId);
        Check(history.RouteOf(restored)==routeId && history.RouteOf(SeString.Parse(original))==0,"route association uses the owned SeString instance, never text, timestamp or reused event identity");
        history.Clear(); Check(history.RouteOf(restored)==0,"history clear releases original-to-route associations");
        Check(!history.TryAdd(id=>record with { Id=id, MapContext=new byte[History.MaxRecordBytes+1] },out _),"coordinate metadata is included in the per-record memory limit");
    }
}
