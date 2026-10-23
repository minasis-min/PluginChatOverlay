using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using PluginChatOverlay;

internal static class GameLogChecks
{
    public static void Run()
    {
        var settings = SettingsStore.Parse("{\"Enabled\":true,\"HistoryLimit\":75,\"Tabs\":[{\"Id\":\"legacy\",\"Name\":\"旧タブ\",\"IncludeAll\":true,\"Plugins\":[\"ExistingPlugin\"]}]}");
        Check(settings.Enabled && !settings.CopyGameLogs && settings.HistoryLimit == 75 && settings.GameHistoryLimit == 100
            && !settings.Tabs[0].GameLogs.Enabled && settings.Tabs[0].GameLogs.Rows.Count == 0 && settings.Tabs[0].Plugins.Single() == "ExistingPlugin",
            "old settings keep plugin filters and copy stays off with its own default history");
        settings.CopyGameLogs = true; settings.GameHistoryLimit = 24;
        settings.Tabs[0].GameLogs = new() { Enabled = true, Rows = [1, 51, 58, 999] };
        var saved = SettingsStore.Parse(JsonSerializer.Serialize(settings));
        Check(saved.CopyGameLogs && saved.GameHistoryLimit == 24 && saved.Tabs[0].GameLogs.Rows.SetEquals([1u,51u,58u,999u]),
            "native filter selections round-trip including unknown IDs for future compatibility");
        settings.GameHistoryLimit = int.MaxValue; settings.Tabs[0].GameLogs.Rows = null!; settings.Normalize();
        Check(settings.GameHistoryLimit == 1000 && settings.Tabs[0].GameLogs.Rows.Count == 0, "native settings tolerate null and bound retained history");

        var catalog = Fixture();
        var filter = new GameLogFilter { Enabled = true, Rows = [51] };
        Check(catalog.Matches(filter,41,1,5) && !catalog.Matches(filter,41,5,1), "damage-dealt source relation is independent of damage-received target relation");
        filter.Rows = [58];
        Check(catalog.Matches(filter,41,5,1) && !catalog.Matches(filter,41,1,5), "damage-received filter uses the target mask");
        filter.Rows = [3, 4];
        Check(catalog.Matches(filter,12,0,0) && catalog.Matches(filter,13,0,0) && catalog.Matches(filter,32,0,0),
            "one Tell choice covers both directions and Party covers cross-world Party");
        filter.Enabled = false;
        Check(!catalog.Matches(filter,13,0,0), "disabled tab never matches native logs");
        filter.Enabled = true;
        Check(!catalog.Matches(filter,13,16,0) && !catalog.Matches(filter,13,-1,0) && !catalog.Matches(filter,999,0,0), "unknown relations and log kinds fail closed for copy only");

        var plan = catalog.BuildSelection([new() { Enabled=true, Rows=[1,51] },new() { Enabled=false, Rows=[58] }]);
        Check(plan.Matches(10,0,0) && plan.Matches(41,1,5) && !plan.Matches(41,5,1), "receive plan unions only enabled tab choices and preserves source masks");
        var mutable = new GameLogFilter {Enabled=true,Rows=[1]};
        var snapshotPlan = catalog.BuildSelection([mutable]); mutable.Rows.Clear();
        Check(snapshotPlan.Matches(10,0,0) && !catalog.BuildSelection([mutable]).Matches(10,0,0), "receive plan remains immutable during UI edits and updates on rebuild");
        var history = new History(); history.ApplyLimit(10); history.ApplyGameLimit(10);
        history.TryAdd(id => Record(id, ChatRecordOrigin.Plugin), out var pluginId);
        for(var i=0;i<100;i++) history.TryAdd(id => Record(id,ChatRecordOrigin.Game),out _);
        Check(history.Snapshot().Length == 11 && history.Snapshot()[0].Id == pluginId, "battle traffic cannot evict plugin history");
        history.ApplyGameLimit(20);
        for(var i=0;i<20;i++) history.TryAdd(id => Record(id,ChatRecordOrigin.Plugin),out _);
        Check(history.Snapshot().Count(r=>r.Origin==ChatRecordOrigin.Game)==10 && history.Snapshot().Count(r=>r.Origin==ChatRecordOrigin.Plugin)==10,
            "plugin traffic cannot evict game history");
        Check(history.Snapshot().Select(r=>r.Id).SequenceEqual(history.Snapshot().Select(r=>r.Id).Order()), "mixed history has one monotonic display sequence");
        history.Clear(); history.ApplyGameLimit(1000);
        for(var i=0;i<300;i++) history.TryAdd(id => Record(id,ChatRecordOrigin.Game) with { Body = new byte[History.MaxRecordBytes] },out _);
        history.TryAdd(id => Record(id,ChatRecordOrigin.Plugin), out pluginId);
        Check(history.Snapshot().Count(r=>r.Origin==ChatRecordOrigin.Game)==256 && history.Snapshot().Any(r=>r.Id==pluginId), "native byte cap is independent of the plugin bucket");
        history.ApplyGameLimit(10);
        Check(history.Snapshot().Length==11, "native limit shrink trims only native records immediately");
        history.Clear();
        Check(history.Snapshot().Length==0, "history clear releases both origins");
        var flags=BindingFlags.Static|BindingFlags.NonPublic;
        var detour=typeof(CaptureCallProbe).GetMethod("Detour",flags)!;
        var update=typeof(CaptureCallProbe).GetMethod("UpdateQueue",flags)!;
        var origin=new QueueOrigin(detour,update);
        Check(origin.Classify([detour,update])==ChatCallOrigin.Plugin && origin.Classify([detour])==ChatCallOrigin.Game
            && origin.Classify([detour,detour,update])==ChatCallOrigin.Unknown && origin.Classify([update])==ChatCallOrigin.Unknown,
            "origin classification rejects recursion and distinguishes direct game callbacks from the plugin queue");
        var reusedCaptureOrigin=ChatCallOrigin.Unknown;
        CaptureCallProbe.Call(()=>reusedCaptureOrigin=origin.ClassifyCurrentCall());
        Check(reusedCaptureOrigin==ChatCallOrigin.Plugin,"optimized final-event callback retains positive queue origin proof");
        string Status(bool enabled = true, bool catalogAvailable = true, bool compatible = true,
            bool drawingFault = false, bool subscribed = true, bool selected = true) =>
            GameLogCapture.Diagnostic(enabled, catalogAvailable, "catalog-problem", compatible, "capture-problem", drawingFault, subscribed, selected);
        Check(Status(catalogAvailable:false).Contains("catalog-problem") && Status(compatible:false).Contains("capture-problem")
            && Status(drawingFault:true).Contains("描画エラー") && Status(subscribed:false).Contains("購読"),
            "copy diagnostic reports each runtime blocker even when filter data alone is available");
        Check(Status(enabled:false).Contains("OFF") && Status(selected:false).Contains("待機中") && Status().StartsWith("有効")
            && !GameLogSelection.Empty.HasSelection && snapshotPlan.HasSelection,
            "copy diagnostic distinguishes disabled, empty selection and active reception");
        CaptureChecks();
        TabCacheChecks(catalog);
    }
    private static ChatRecord Record(long id, ChatRecordOrigin origin) => new(id,57,0,0,0,[],[(byte)'x'],"x") { Origin=origin };
    private static GameLogCatalog Fixture() => new([
        new(1,"Say",10,ushort.MaxValue,ushort.MaxValue,1,1),
        new(3,"Tell",12,ushort.MaxValue,ushort.MaxValue,1,4),
        new(4,"Party",14,ushort.MaxValue,ushort.MaxValue,1,5),
        new(51,"自分がダメージを与えた",41,2,ushort.MaxValue,4,1),
        new(58,"自分がダメージを受けた",41,ushort.MaxValue,2,4,8)]);
    private static void CaptureChecks()
    {
        var chat=DispatchProxy.Create<IChatGui,ServiceProxy>();
        var handlers=new Dictionary<string,Delegate>();
        ((ServiceProxy)chat).Result=(m,a)=> { if(m.Name.StartsWith("add_")) handlers[m.Name[4..]]=(Delegate)a![0]!; if(m.Name.StartsWith("remove_")) handlers.Remove(m.Name[7..]); return null; };
        var framework=DispatchProxy.Create<IFramework,ServiceProxy>();
        var onThread=true;
        ((ServiceProxy)framework).Result=(m,_)=>m.Name=="get_IsInFrameworkUpdateThread" ? onThread : null;
        var log=DispatchProxy.Create<IPluginLog,ServiceProxy>(); var history=new History();
        var enabled=false; var wanted=true; var classification=ChatCallOrigin.Game; var traces=0; var reads=0; var handled=false;
        var text="first"; var throwCopy=false; var mutationCalls=0;
        var message=DispatchProxy.Create<IChatMessage,ServiceProxy>();
        ((ServiceProxy)message).Result=(m,_)=>
        {
            reads++;
            if(m.Name.StartsWith("set_") || m.Name=="PreventOriginal") mutationCalls++;
            return m.Name switch {
                "get_IsHandled"=>handled, "get_LogKind"=>XivChatType.Say,
                "get_SourceKind" or "get_TargetKind" => XivChatRelationKind.None,
                "get_Timestamp"=>0, "get_Sender"=>new SeString(),
                "get_Message" => throwCopy ? throw new IOException("fixture encode failure") : new SeString(new TextPayload(text)),
                _=>throw new Exception(m.Name)
            };
        };
        using var capture=new GameLogCapture(chat,framework,log,history,()=>enabled,()=>{traces++;return classification;},(_,_,_)=>wanted,(_,_,b)=>b);
        var handler=(IChatGui.OnChatMessageDelegate)handlers["ChatMessageUnhandled"];
        handler(message);
        Check(reads==0 && traces==0 && history.Snapshot().Length==0, "copy OFF reads no message and allocates no stack trace");
        enabled=true; wanted=false; handler(message);
        Check(traces==0 && history.Snapshot().Length==0, "unselected game logs skip stack traces and snapshots");
        wanted=true; classification=ChatCallOrigin.Plugin; handler(message);
        Check(history.Snapshot().Length==0, "queued plugin output is excluded while separation is OFF");
        classification=ChatCallOrigin.Unknown; handler(message);
        Check(history.Snapshot().Length==0, "recursive or unsupported caller cannot enter native history");
        classification=ChatCallOrigin.Game; handler(message); text="second"; handler(message);
        Check(history.Snapshot().Select(r=>r.Plain).SequenceEqual(["first","second"]) && history.Snapshot().All(r=>r.Origin==ChatRecordOrigin.Game),
            "reused mutable events produce independent owned copies without final-event replacement");
        handled=true; handler(message); handled=false; onThread=false; handler(message); onThread=true;
        Check(history.Snapshot().Length==2, "hidden messages and callbacks outside the framework thread are not copied");
        throwCopy=true; handler(message); throwCopy=false; text="after failure"; handler(message);
        Check(capture.Failures==1 && history.Snapshot().Length==3 && history.Snapshot()[0].Plain=="first" && mutationCalls==0,
            "copy failure leaves vanilla untouched and cannot poison the next message");
        capture.Dispose(); handler(message);
        Check(handlers.Count==0 && history.Snapshot().Length==3, "native subscription is removed and stale callbacks are harmless after disposal");
        using var module=Mono.Cecil.ModuleDefinition.ReadModule(typeof(GameLogCapture).Assembly.Location);
        var calls=module.Types.Single(t=>t.FullName==typeof(GameLogCapture).FullName).Methods.Where(m=>m.HasBody)
            .SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<Mono.Cecil.MethodReference>();
        Check(!calls.Any(c=>c.Name is "PreventOriginal" or "set_Message" or "set_Sender" or "Print" or "PrintError"),
            "native capture IL has no suppression, chat mutation or game reprint path");
    }
    private static void TabCacheChecks(GameLogCatalog catalog)
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        var plugin=(Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
        typeof(Plugin).GetField("gameLogCatalog",flags)!.SetValue(plugin,catalog);
        var views=Activator.CreateInstance(typeof(Plugin).GetField("views",flags)!.FieldType)!;
        typeof(Plugin).GetField("views",flags)!.SetValue(plugin,views);
        var palette=new ChatPalette(DispatchProxy.Create<IDataManager,ServiceProxy>(),DispatchProxy.Create<IPluginLog,ServiceProxy>());
        var tab=new ChatTab { IncludeAll=true };
        var docs=new[]{new DisplayDocument(Record(1,ChatRecordOrigin.Plugin),"plugin",new(),palette,0),
            new DisplayDocument(new(2,10,0,0,0,[],[(byte)'a'],"a"){Origin=ChatRecordOrigin.Game},"",new(){ShowTime=false},palette,0)};
        var method=typeof(Plugin).GetMethod("TabDocuments",flags)!;
        DisplayDocument[] Filter()=>(DisplayDocument[])method.Invoke(plugin,[tab,docs])!;
        Check(Filter().Length==1 && Filter()[0].Record.Origin==ChatRecordOrigin.Plugin, "legacy IncludeAll applies to plugins only");
        tab.GameLogs.Enabled=true; tab.GameLogs.Rows.Add(1);
        Check(Filter().Length==2, "changing game filters invalidates the cached tab without a new message");
        tab.IncludeAll=false;
        Check(Filter().Length==1 && Filter()[0].Record.Origin==ChatRecordOrigin.Game, "game-only tabs do not depend on plugin selection");
        tab.GameLogs.Rows.Clear(); Check(Filter().Length==0,"clearing native choices invalidates cached rows");
        var gameDocument=docs[1]; gameDocument.CacheText();
        Check(gameDocument.Text=="a", "preformatted native record is rendered once without plugin tag, sender or duplicate color prefix");
    }
    public static void RunGameData(string directory)
    {
        using var game=new Lumina.GameData(directory,new Lumina.LuminaOptions {DefaultExcelLanguage=Lumina.Data.Language.Japanese});
        var rows=game.Excel.GetSheet<Lumina.Excel.Sheets.LogFilter>()!;
        var catalog=new GameLogCatalog(rows.Select(r=>new GameLogFilterEntry(r.RowId,r.Name.ExtractText(),r.LogKind,r.Caster,r.Target,r.Category,r.DisplayOrder)));
        Check(catalog.Category(1).Length==27 && catalog.Category(2).Length==31,"installed game filter data matches 27 chat and 31 announcement choices inspected with MCP");
        Check(Enumerable.Range(4,11).All(c=>catalog.Category(c).Length==16),"installed game data supplies all 11 subjects with 16 independent battle filters");
        foreach(var entry in catalog.Entries)
        {
            var filter=new GameLogFilter {Enabled=true,Rows=[entry.Id]};
            for(var source=0;source<12;source++) for(var target=0;target<12;target++)
            {
                var expected=(entry.Caster&(1<<source))!=0 && (entry.Target&(1<<target))!=0;
                if(catalog.Matches(filter,entry.Kind,source,target)!=expected) throw new Exception("native relation mask mismatch at row "+entry.Id);
            }
        }
        Check(true,"all 234 native choices checked against the installed game's caster/target masks (33696 combinations)");
        var kinds=game.Excel.GetSheet<Lumina.Excel.Sheets.LogKind>()!;
        foreach(var kind in new uint[]{10,12,13,14,32,41,57}) Console.WriteLine("FORMAT "+kind+" "+kinds.GetRow(kind).Format.ToMacroString());
    }
    private static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
}
