using System.Reflection;
using System.Runtime.CompilerServices;
using PluginChatOverlay;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;


var flags=BindingFlags.Static|BindingFlags.NonPublic;
var detour=typeof(Probe).GetMethod("Detour",flags)!;
var update=typeof(Probe).GetMethod("UpdateQueue",flags)!;
var origin=new QueueOrigin(detour,update);
void Check(bool value,string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS "+name); }
Check(origin.Matches([detour,update]),"queue proof");
Check(!origin.Matches([detour]),"missing caller fails open");
Check(!origin.Matches([detour,null,update]),"native boundary fails open");
Check(!origin.Matches([detour,detour,update]),"recursive event rejected");
Check(!origin.Matches([detour,typeof(Probe).GetMethod("Game",flags),update]),"nested game call rejected");
Probe.Origin=origin;
Check(Probe.Queue(),"optimized runtime stack queue");
Check(!Probe.GameCall(),"optimized runtime stack game");
var clock=System.Diagnostics.Stopwatch.StartNew();
for(var i=0;i<10000;i++) if(!Probe.Queue()) throw new Exception("tiered stack proof");
Console.WriteLine($"PASS 10000 tiered queue calls: {clock.Elapsed.TotalMilliseconds:F1} ms (synthetic, not in-game)");

var history = new History();
history.ApplyLimit(History.MaxRecords);
ChatRecord Record(long id, int size = 1) => new(id, 56, 0, 0, 0, [], new byte[size], "owned");
var cacheHistory = new History();
cacheHistory.TryAdd(id => Record(id), out var cacheId);
var cached = cacheHistory.RenderSnapshot();
Check(ReferenceEquals(cached, cacheHistory.RenderSnapshot()), "unchanged rendering reuses history snapshot");
cacheHistory.Replace(cacheId, Record(cacheId, 2));
var replacedCache = cacheHistory.RenderSnapshot();
Check(!ReferenceEquals(cached, replacedCache) && cached[0].Bytes == 1 && replacedCache[0].Bytes == 2, "replacement invalidates cache without changing previously published snapshot");
cacheHistory.TryAdd(id => Record(id, History.MaxRecordBytes + 1), out _);
Check(ReferenceEquals(replacedCache, cacheHistory.RenderSnapshot()), "rejected capture preserves cached snapshot");
cacheHistory.Remove(cacheId);
Check(cacheHistory.RenderSnapshot().Length == 0 && replacedCache.Length == 1, "rollback invalidates cache without modifying old snapshot");
for (var i = 0; i < 100; i++) cacheHistory.TryAdd(id => Record(id), out _);
var beforeTrim = cacheHistory.RenderSnapshot(); cacheHistory.ApplyLimit(10);
Check(cacheHistory.RenderSnapshot().Length == 10 && beforeTrim.Length == 100, "batch trimming invalidates cache and retains snapshot ownership");
cacheHistory.Clear();
Check(cacheHistory.RenderSnapshot().Length == 0, "clear invalidates render cache");
var allocationHistory = new History(); allocationHistory.ApplyLimit(10000);
for (var i = 0; i < 10000; i++) allocationHistory.TryAdd(id => Record(id), out _);
allocationHistory.RenderSnapshot();
var allocationStart = GC.GetAllocatedBytesForCurrentThread();
for (var i = 0; i < 1000; i++) allocationHistory.Snapshot();
var copyingBytes = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
allocationStart = GC.GetAllocatedBytesForCurrentThread();
for (var i = 0; i < 1000; i++) allocationHistory.RenderSnapshot();
var cachedBytes = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
Check(cachedBytes == 0 && copyingBytes >= 8000000, $"1000 records x 1000 unchanged snapshots: copy {copyingBytes} bytes, cached {cachedBytes} bytes (synthetic allocation measurement)");
for(var i=0;i<1200;i++) CheckQuiet(history.TryAdd(id=>Record(id), out _));
Check(history.Snapshot().Length==1000 && history.Snapshot()[0].Id==201,"bounded count and monotonic ids");
Check(!history.TryAdd(id=>Record(id,History.MaxRecordBytes+1),out _) && history.Snapshot().Length==1000,"oversize fail open preserves history");
try { history.TryAdd(_=>throw new IOException("copy failure"),out _); } catch(IOException) { }
Check(history.Snapshot().Length==1000,"copy exception before suppression leaves history intact");
history.ApplyLimit(10000);
history.Clear();
for(var i=0;i<400;i++) CheckQuiet(history.TryAdd(id=>Record(id,History.MaxRecordBytes),out _));
Check(history.Snapshot().Sum(r=>r.Bytes)<=History.MaxHistoryBytes,"byte capacity bounds bulk logs");
var item=history.Snapshot()[0];
history.Replace(item.Id,Record(item.Id,4));
Check(history.Snapshot().Length==256 && history.Snapshot()[0].Body.Length==4,"final-event replacement does not duplicate");
history.Remove(item.Id);
Check(history.Snapshot().Length==255,"unhandled rollback removes overlay duplicate");
history.ApplyLimit(10);
Check(history.Snapshot().Length==10,"limit reduction evicts immediately");
history.Clear();
Parallel.For(0,10000,_=>CheckQuiet(history.TryAdd(id=>Record(id),out _)));
Check(history.Snapshot().Length==10 && history.Snapshot().Select(r=>r.Id).Distinct().Count()==10,"concurrent bounded ownership");
var settings=new Settings {BackgroundAlpha=float.NaN,FontSize=float.PositiveInfinity,HistoryLimit=int.MaxValue,Width=-1};
settings.Normalize();
Check(settings.BackgroundAlpha==.8f && settings.FontSize==17 && settings.HistoryLimit==1000 && settings.Width==250,"invalid settings normalized");
static void CheckQuiet(bool ok) { if(!ok) throw new Exception("unexpected rejection"); }

if(args.Length>0) MetadataAudit.Run(args[0]);

var firstMap = new MapLinkPayload(132u,1u,1000,2000);
var secondMap = new MapLinkPayload(132u,1u,3000,4000);
var prefix = new SeStringBuilder().AddText("日本語前置き ").Build().Encode();
var firstLink = new SeStringBuilder().Add(firstMap).AddUiForeground(45).AddText("最初のリンク").AddUiForegroundOff().Add(RawPayload.LinkTerminator).Build().Encode();
var gap = new SeStringBuilder().AddText(" 折り返し候補の文章 ").Build().Encode();
var secondLink = new SeStringBuilder().Add(secondMap).AddText("二つ目のリンク").Add(RawPayload.LinkTerminator).Build().Encode();
byte[] combined = [..prefix,..firstLink,..gap,..secondLink];
Check(Links.Envelope(combined,prefix.Length).Encode().SequenceEqual(firstLink),"first link passes exact envelope including color and terminator");
Check(Links.Envelope(combined,prefix.Length+firstLink.Length+gap.Length).Encode().SequenceEqual(secondLink),"second link independent of previous link");
var malformed = new SeStringBuilder().Add(firstMap).AddText("unterminated").Build().Encode();
var rejected=false;
try { Links.Envelope(malformed,0); } catch(FormatException) { rejected=true; }
Check(rejected,"unterminated link fails safely");

var pluginLink = new DalamudLinkPayload();
typeof(DalamudLinkPayload).GetProperty("Plugin")!.SetValue(pluginLink,"FixturePlugin");
typeof(DalamudLinkPayload).GetProperty("CommandId")!.SetValue(pluginLink,701u);
typeof(DalamudLinkPayload).GetProperty("ExtraString")!.SetValue(pluginLink,"日本語の付加データ");
var pluginBytes = new SeStringBuilder().Add(pluginLink).AddText("設定を開く").Add(RawPayload.LinkTerminator).Build().Encode();
var registry=new Dictionary<(string PluginName,uint CommandId),Action<uint,SeString>>();
var calls=0;
registry.Add(("FixturePlugin",701u),(command,message)=> { CheckQuiet(command==701u && message.Encode().SequenceEqual(pluginBytes)); calls++; });
var linkNotices=new List<SeString>();
var fakeChat=DispatchProxy.Create<IChatGui,ServiceProxy>();
((ServiceProxy)(object)fakeChat).Result=(method,arguments)=> { if(method.Name=="get_RegisteredLinkHandlers") return registry; if(method.Name=="Print" && arguments?[0] is SeString notice) linkNotices.Add(notice); return null; };
var fakeGame=DispatchProxy.Create<IGameGui,ServiceProxy>();
var mapCalls=0;
((ServiceProxy)(object)fakeGame).Result=(method,_)=> { if(method.Name=="OpenMapWithMapLink") { mapCalls++; return true; } return null; };
var fakeLog=DispatchProxy.Create<IPluginLog,ServiceProxy>();
long linkClock=0;
var links=new Links(fakeChat,fakeGame,fakeLog,DispatchProxy.Create<IFramework,ServiceProxy>(),()=>linkClock);
links.Activate(pluginBytes,0);
Check(calls==1 && links.Notice.Length==0,"plugin callback gets original command id, extras and exact link message (stub handler)");
registry.Clear();
links.Activate(pluginBytes,0);
Check(calls==1 && links.Notice.Contains("利用できません"),"removed handler not retained and unavailable notice shown");
Check(linkNotices.Count==1 && linkNotices[0].TextValue.Contains("設定を開く") && linkNotices[0].Payloads.All(p=>p is TextPayload),"failed action reports clicked link name as plain chat text once");
linkClock=4999;
Check(links.Notice.Length>0,"link notice remains visible before timeout");
linkClock=5000;
Check(links.Notice.Length==0,"link notice expires without further interaction");
links.Activate(pluginBytes,0);
linkClock=9000;
links.Activate(pluginBytes,0);
linkClock=10001;
Check(links.Notice.Length>0,"a new link notice renews its own timeout");
linkClock=14000;
Check(links.Notice.Length==0,"renewed link notice also expires");
registry.Add(("FixturePlugin",701u),(_,_)=>throw new Exception("fixture handler failure"));
links.Activate(pluginBytes,0);
Check(links.Notice.Contains("失敗"),"handler exception contained");
links.Activate(firstLink,0);
Check(mapCalls==1 && links.Notice.Length==0,"map payload routed to public API (stub game)");

var itemFailure=new SeStringBuilder().Add(new ItemPayload(100u)).AddText("Fixture item").Add(RawPayload.LinkTerminator).Build();
var beforeLinkNotices=linkNotices.Count;
links.Activate(itemFailure.Encode(),0);
Check(linkNotices.Count==beforeLinkNotices+1 && linkNotices[^1].TextValue.Contains("アイテム「Fixture item」") && linkNotices[^1].Payloads.All(p=>p is TextPayload),"unsupported item action reports its name and kind without clickable payloads");
Check(!Links.Describe(new SeString(new TextPayload("line1\nline2\t"))).Contains('\n'),"link descriptions remove chat control characters");
registry[("FixturePlugin",701u)]=(_,message)=> { message.Payloads.Clear(); message.Payloads.Add(null!); throw new Exception("fixture corrupt callback"); };
links.Activate(pluginBytes,0);
Check(links.Notice.Contains("名前を取得できません") && links.Notice.Contains("失敗"),"malformed callback output cannot escape failure reporting");
PresentationChecks.Run();
NativeWindowChecks.Run();
NativeChatVisibilityChecks.Run();
CopyShortcutChecks.Run();
TooltipChecks.Run();
TooltipCleanupChecks.Run();
SettingsStoreChecks.Run();
CleanupChecks.Run();
CaptureBoundaryChecks.Run();
RenderCacheChecks.Run();
SelectionIndexChecks.Run();
CompatibilityChecks.Run();
AppearanceChecks.Run();
AltMoveChecks.Run();
UiChecks.Run();
ChatStyleChecks.Run();

static class Probe {
    public static QueueOrigin Origin=null!;
    [MethodImpl(MethodImplOptions.NoInlining)] private static bool Detour()=>Origin.IsQueueCall();
    [MethodImpl(MethodImplOptions.NoInlining)] private static bool UpdateQueue() { var result=Detour(); GC.KeepAlive(Origin); return result; }
    [MethodImpl(MethodImplOptions.NoInlining)] private static bool Game() { var result=Detour(); GC.KeepAlive(Origin); return result; }
    public static bool Queue()=>UpdateQueue();
    public static bool GameCall()=>Game();
}

public class ServiceProxy : DispatchProxy {
    public Func<MethodInfo,object?[]?,object?> Result=(_,_)=>null;
    protected override object? Invoke(MethodInfo? method,object?[]? args)=>Result(method!,args);
}
