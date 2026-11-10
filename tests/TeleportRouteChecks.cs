using System.Reflection;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Lumina.Text.ReadOnly;
using PluginChatOverlay;
internal static class TeleportRouteChecks
{
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("PASS " + label); }
    private static DalamudLinkPayload Link(uint command, string owner = "AetheryteLinkInChat")
    {
        var link = new DalamudLinkPayload();
        typeof(DalamudLinkPayload).GetProperty("Plugin")!.SetValue(link, owner);
        typeof(DalamudLinkPayload).GetProperty("CommandId")!.SetValue(link, command);
        return link;
    }
    private static SeString Route(string owner = "AetheryteLinkInChat")
    {
        var route = new SeString(new TextPayload("\n"), new IconPayload(BitmapFontIcon.Aetheryte), new UIForegroundPayload(69));
        route.Payloads.AddRange([new UIForegroundPayload(500), new UIGlowPayload(501), new TextPayload("→"), UIGlowPayload.UIGlowOff, UIForegroundPayload.UIForegroundOff]);
        route.Payloads.AddRange([Link(0, owner), new TextPayload("最寄りの地点"), new RawPayload([2, 0x27, 2, 0x60, 3]), RawPayload.LinkTerminator, UIForegroundPayload.UIForegroundOff,
            new TextPayload(" ["), new IconPayload(BitmapFontIcon.Aetheryte), new UIForegroundPayload(69)]);
        route.Payloads.AddRange([new UIForegroundPayload(500), new UIGlowPayload(501), new TextPayload("→"), UIGlowPayload.UIGlowOff, UIForegroundPayload.UIForegroundOff]);
        route.Payloads.AddRange([Link(1, owner), new TextPayload("Lifestream"), new RawPayload([2, 0x27, 2, 0x61, 3]), RawPayload.LinkTerminator, UIForegroundPayload.UIForegroundOff, new TextPayload("]")]);
        return route;
    }
    private static bool Owned(DalamudLinkPayload link) => link.Plugin == "AetheryteLinkInChat" && link.CommandId <= 1;
    public static void Run()
    {
        var original = new SeString(new TextPayload("元の発言 "), new MapLinkPayload(1, 1, 100, 200), RawPayload.LinkTerminator, new TextPayload(" 元の末尾")).Encode();
        var addition = Route().Encode(); var modified = original.Concat(addition).ToArray();
        Check(TeleportRoute.TryRestore(10, original, modified, Owned, out var restored) && restored.Encode().AsSpan().SequenceEqual(original), "ALI route suffix restores the original player statement and map payload byte-for-byte");
        var head = new SeString(new TextPayload("元の発言 "), new MapLinkPayload(1, 1, 100, 200), RawPayload.LinkTerminator).Encode();
        var inline = head.Concat(addition.AsSpan(1).ToArray()).Concat(new SeString(new TextPayload(" 元の末尾")).Encode()).ToArray();
        Check(TeleportRoute.TryRestore(10, original, inline, Owned, out restored) && restored.Encode().AsSpan().SequenceEqual(original), "payload-aligned inline route preserves the original text after the coordinates");
        Check(!TeleportRoute.TryRestore(10, original, modified, _=>false, out _) && !TeleportRoute.TryRestore(56, original, modified, Owned, out _), "missing route handlers and non-player notifications remain untouched");
        Check(!TeleportRoute.TryRestore(10, original, original.Concat(Route("OtherPlugin").Encode()).ToArray(), Owned, out _), "another plugin's registered link is never removed as an ALI route");
        Check(!TeleportRoute.TryRestore(10, original, modified.Concat(new byte[]{(byte)'X'}).ToArray(), Owned, out _), "additional trailing output fails open instead of removing unrelated text");
        var edited = modified.ToArray(); edited[0] = (byte)'X';
        Check(!TeleportRoute.TryRestore(10, original, edited, Owned, out _) && !TeleportRoute.TryRestore(10, modified, modified, Owned, out _), "original edits and already-present links are not mistaken for new additions");
        Check(!SettingsStore.Parse("{\"SeparateTeleportRoutes\":true}").Enabled, "obsolete route-specific preference cannot enable notification separation");
        Capture(original, modified);
    }
    private static void Capture(byte[] original, byte[] modified)
    {
        var handlers=new Dictionary<string,Delegate>();
        var chat=DispatchProxy.Create<IChatGui,ServiceProxy>();
        ((ServiceProxy)chat).Result=(m,a)=> { if(m.Name.StartsWith("add_")) handlers[m.Name[4..]]=(Delegate)a![0]!; if(m.Name.StartsWith("remove_")) handlers.Remove(m.Name[7..]); return null; };
        var onThread=true; var enabled=true; var owns=true; var failRead=false; var failSet=false; var handled=false; var writes=0;
        var classification=ChatCallOrigin.Game; var body=SeString.Parse(modified);
        var framework=DispatchProxy.Create<IFramework,ServiceProxy>();
        ((ServiceProxy)framework).Result=(m,_)=>m.Name=="get_IsInFrameworkUpdateThread" ? onThread : null;
        var message=DispatchProxy.Create<IHandleableChatMessage,ServiceProxy>();
        ((ServiceProxy)message).Result=(m,a)=>m.Name switch
        {
            "get_IsHandled"=>handled, "get_LogKind"=>XivChatType.Say,
            "get_SourceKind" or "get_TargetKind"=>XivChatRelationKind.None,
            "get_Timestamp"=>0, "get_Sender"=>new SeString(),
            "get_Message"=>failRead ? throw new IOException("read failed") : body,
            "get_OriginalMessage"=>new ReadOnlySeString(original),
            "set_Message"=>Set((SeString)a![0]!), "PreventOriginal"=>Prevent(),
            _=>throw new Exception(m.Name)
        };
        object? Set(SeString value) { if(failSet) throw new IOException("set failed"); writes++; body=value; return null; }
        object? Prevent() { handled=true; return null; }
        var history=new History(); var log=DispatchProxy.Create<IPluginLog,ServiceProxy>();
        using var capture=new ChatCapture(chat,framework,log,history,()=>enabled,()=>classification,link=>owns && Owned(link));
        var check=(IChatGui.OnHandleableChatMessageDelegate)handlers["CheckMessageHandled"];
        var copyMap=false;
        using var native=new GameLogCapture(chat,framework,log,history,()=>copyMap,()=>classification,(_,_,_)=>true,(_,_,bytes)=>bytes,(_,_,_,bytes)=>PositionMessages.HasMapLink(bytes),Owned);
        var finish=(IChatGui.OnChatMessageDelegate)handlers["ChatMessageUnhandled"];
        void Start() { body=SeString.Parse(modified); handled=false; }
        Start(); check(message); finish(message);
        var route=history.Snapshot().Single();
        Check(route.Origin==ChatRecordOrigin.Plugin && route.PluginOwner=="AetheryteLinkInChat" && !PositionMessages.HasMapLink(route.Body)
            && SeString.Parse(route.Body).Payloads.OfType<DalamudLinkPayload>().Count()==2 && body.Encode().AsSpan().SequenceEqual(original),
            "plugin route appears with game/map copying OFF and preserves both clickable links while restoring native speech");
        Check(PositionMessages.HasMapLink(route.MapContext), "separated route retains only its original coordinate context");
        Check(!handled && capture.AppendedRoutes==1 && native.Copied==0 && route.Plain.Contains("Lifestream"), "route separation never suppresses the player statement or requires a game-log selection");
        history.Clear(); copyMap=true; Start(); check(message); finish(message);
        Check(history.Snapshot().Length==2 && history.Snapshot().Count(r=>r.Origin==ChatRecordOrigin.Plugin)==1
            && history.Snapshot().Single(r=>r.Origin==ChatRecordOrigin.Game).Body.AsSpan().SequenceEqual(original)
            && history.Snapshot().Single(r=>r.Origin==ChatRecordOrigin.Game).RelatedRouteId==history.Snapshot().Single(r=>r.Origin==ChatRecordOrigin.Plugin).Id,
            "map opt-in adds the original player statement separately without duplicating the plugin route");
        history.Clear(); enabled=false; Start(); check(message); finish(message);
        Check(body.Encode().AsSpan().SequenceEqual(modified) && history.Snapshot().Single().Origin==ChatRecordOrigin.Game
            && history.Snapshot()[0].Body.AsSpan().SequenceEqual(original), "notification separation OFF retains native routes while map copies still contain only original speech");
        history.Clear(); enabled=true; copyMap=false; var before=writes;
        foreach(var condition in new[]{"unknown","handled","thread","owner"})
        {
            Start(); classification=condition=="unknown" ? ChatCallOrigin.Unknown : ChatCallOrigin.Game;
            handled=condition=="handled"; onThread=condition!="thread"; owns=condition!="owner";
            check(message);
            Check(writes==before && history.Snapshot().Length==0, "route remains native for "+condition);
            classification=ChatCallOrigin.Game; handled=false; onThread=owns=true;
        }
        Start(); failRead=true; check(message); failRead=false;
        Check(writes==before && history.Snapshot().Length==0, "failed route copying does not remove native output");
        Start(); failSet=true; check(message); failSet=false;
        Check(history.Snapshot().Length==0 && body.Encode().AsSpan().SequenceEqual(modified), "failed native restoration removes its tentative plugin record");
        Start(); check(message); var previous=history.Snapshot().Single().Id;
        // No final callback for A, then B bypasses checking because it is pre-handled.
        Start(); handled=true; check(message); handled=false;
        Check(history.Snapshot().Single().Id==previous, "route capture has no pending ID to reuse for a later message");
        history.Clear(); Start(); classification=ChatCallOrigin.Plugin; check(message);
        Check(handled && writes==before+1 && history.Snapshot().Length==1 && history.Snapshot()[0].Body.AsSpan().SequenceEqual(modified),
            "queued Faloop-style output follows ordinary notification separation once without route splitting");
        capture.Dispose(); native.Dispose(); before=writes; var count=history.Snapshot().Length; check(message); finish(message);
        Check(handlers.Count==0 && writes==before && history.Snapshot().Length==count, "notification and native-copy subscriptions are removed and stale callbacks are harmless");
    }
}
