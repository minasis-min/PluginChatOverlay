using System.Reflection;
using Dalamud.Game.Chat;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Dalamud.Game.Text.SeStringHandling;

namespace PluginChatOverlay;

internal sealed class ChatCapture : IDisposable
{
    private readonly IChatGui chat;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly History history;
    private readonly Func<bool> enabled;
    private readonly QueueOrigin? origin;
    private readonly Func<ChatCallOrigin>? classifyOverride;
    private readonly Func<DalamudLinkPayload,bool> routeLinkOwner;
    private bool disposed;
    private bool subscribed;
    public string Status { get; private set; } = "未初期化";
    public string CompatibilityDiagnostic { get; private set; } = "未確認";
    public bool Compatible => origin != null && subscribed && !disposed;
    public long QueueMatches { get; private set; }
    public long Captured { get; private set; }
    public long Passed { get; private set; }
    public long Failures { get; private set; }
    public long AppendedLoot { get; private set; }
    public long AppendedRoutes { get; private set; }

    public ChatCapture(IChatGui chat, IFramework framework, IPluginLog log, History history, Func<bool> enabled, Func<ChatCallOrigin>? classifyOverride = null, Func<DalamudLinkPayload,bool>? routeLinkOwner = null)
    {
        this.chat = chat; this.framework = framework; this.log = log; this.history = history; this.enabled = enabled; this.classifyOverride=classifyOverride; this.routeLinkOwner=routeLinkOwner ?? (link=>TeleportRoute.OwnsLink(chat,link));
        try
        {
            var assembly = typeof(IChatGui).Assembly;
            if (assembly.GetName().Version?.Major != 15)
                throw new NotSupportedException("Dalamud APIの世代が未対応です。");
            var type = assembly.GetType("Dalamud.Game.Gui.ChatGui", true)!;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var detour = type.GetMethod("HandlePrintMessageDetour", flags)!;
            var update = type.GetMethod("UpdateQueue", flags)!;
            if (detour == null || update == null) throw new MissingMethodException("チャット経路が未対応です。");
            var detourFingerprint = MethodCompatibility.Fingerprint(detour);
            var queueFingerprint = MethodCompatibility.Fingerprint(update);
            CompatibilityDiagnostic = ChatCompatibility.Diagnose(detourFingerprint, queueFingerprint);
            if (!ChatCompatibility.MatchesFingerprints(detourFingerprint, queueFingerprint))
                throw new NotSupportedException(CompatibilityDiagnostic);
            origin = new(detour, update);
            Status = "チャット処理の互換性確認済み: キュー経路確認方式";
            chat.CheckMessageHandled += Check;
            subscribed = true;
        }
        catch (Exception ex)
        {
            if (CompatibilityDiagnostic == "未確認") CompatibilityDiagnostic = "非互換／確認失敗: " + ex.GetType().Name + "。" + ex.Message;
            Cleanup.Run(error => log.Warning(error, "Partial chat initialization cleanup failed"),
                () => chat.CheckMessageHandled -= Check);
            Status = "分離停止: " + ex.Message; log.Warning(ex, "Chat separation unavailable; vanilla output is preserved");
        }
    }

    private void Check(IHandleableChatMessage message)
    {
        if (!Compatible || !framework.IsInFrameworkUpdateThread) return;
        try
        {
            if (!enabled()) return;
            var call=ClassifyCurrentCall();
            if(call!=ChatCallOrigin.Plugin)
            {
                Passed++;
                if(call==ChatCallOrigin.Game)
                {
                    var kind=(int)message.LogKind;
                    if(PositionMessages.IsPlayerKind(kind)) CaptureTeleportRoute(message, kind);
                    else if(kind==62) CaptureLootAppend(message);
                }
                return;
            }
            QueueMatches++;
            if (message.IsHandled) return;
            if (!history.TryAdd(id => Copy(id, message), out var id)) { Passed++; return; }
            // Already owned, parsed and registered; this is the sole suppression point.
            try { message.PreventOriginal(); Captured++; }
            catch { history.Remove(id); throw; }
        }
        catch (Exception ex) { Failures++; log.Error(ex, "Chat capture failed; no new suppression requested"); }
    }
    private void CaptureTeleportRoute(IHandleableChatMessage message, int kind)
    {
        if(message.IsHandled) return;
        var changed=message.Message;
        if(!changed.Payloads.Any(p=>p is MapLinkPayload)) return;
        var original=message.OriginalMessage.AsSpan().Data.ToArray();
        var modified=changed.Encode();
        if(!TeleportRoute.TryRestore(kind,original,modified,routeLinkOwner,out var restored,out var route)) return;
        var owner=SeString.Parse(route).Payloads.OfType<DalamudLinkPayload>().First().Plugin;
        var start=0; while(start<route.Length && route[start] is 10 or 13) start++;
        if(start>0) route=route[start..];
        var plain=SeString.Parse(route).TextValue;
        var mapContext=TeleportRoute.MapContext(original);
        var source=(int)message.SourceKind; var target=(int)message.TargetKind; var timestamp=message.Timestamp;
        if(!history.TryAdd(id=>new(id,kind,source,target,timestamp,[],route,plain) { PluginOwner=owner, MapContext=mapContext },out var recordId)) return;
        try { history.RememberRoute(restored,recordId); message.Message=restored; AppendedRoutes++; }
        catch { history.Remove(recordId); throw; }
    }
    private void CaptureLootAppend(IHandleableChatMessage message)
    {
        if (message.IsHandled) return;
        var original = message.OriginalMessage.AsSpan().Data.ToArray();
        var modified = message.Message.Encode();
        if (!LootAppend.Split((int)message.LogKind, original, modified, out var addition)) return;
        var sender = message.Sender.Encode();
        var restored = SeString.Parse(original);
        var plain = SeString.Parse(addition).TextValue;
        if (!history.TryAdd(id => new(id, (int)message.LogKind, (int)message.SourceKind, (int)message.TargetKind, message.Timestamp,
                sender, addition, message.Sender.TextValue + (sender.Length > 0 ? ": " : "") + plain), out var recordId)) return;
        try
        {
            // Do not PreventOriginal: the game's acquisition message must still be printed.
            message.Message = restored;
            AppendedLoot++;
        }
        catch { history.Remove(recordId); throw; }
    }

    internal ChatCallOrigin ClassifyCurrentCall() => Compatible ? classifyOverride?.Invoke() ?? origin!.ClassifyCurrentCall() : ChatCallOrigin.Unknown;

    private static ChatRecord Copy(long id, IChatMessage message)
    {
        var sender = message.Sender.Encode();
        var body = message.Message.Encode();
        return new(id, (int)message.LogKind, (int)message.SourceKind, (int)message.TargetKind, message.Timestamp,
            sender, body, message.Sender.TextValue + (sender.Length > 0 ? ": " : "") + message.Message.TextValue);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Cleanup.Run(ex => log.Warning(ex, "Chat event cleanup failed"),
            () => chat.CheckMessageHandled -= Check);
    }
}
