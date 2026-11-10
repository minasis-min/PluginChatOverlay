using Dalamud.Game.Chat;
using Dalamud.Plugin.Services;
using Dalamud.Game.Text.SeStringHandling.Payloads;
namespace PluginChatOverlay;
/// <summary>Observes completed game messages without changing native output.</summary>
internal sealed class GameLogCapture : IDisposable
{
    private readonly IChatGui chat;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly History history;
    private readonly Func<bool> enabled;
    private readonly Func<ChatCallOrigin> classify;
    private readonly Func<int, int, int, bool> selected;
    private readonly Func<int, byte[], byte[], byte[]> format;
    private readonly Func<int, int, int, byte[], bool>? selectedBody;
    private readonly Func<DalamudLinkPayload,bool> routeLinkOwner;
    private bool disposed;
    public bool Subscribed { get; private set; }
    public long Copied { get; private set; }
    public long Failures { get; private set; }
    public GameLogCapture(IChatGui chat, IFramework framework, IPluginLog log, History history, Func<bool> enabled,
        Func<ChatCallOrigin> classify, Func<int, int, int, bool> selected, Func<int, byte[], byte[], byte[]> format, Func<int, int, int, byte[], bool>? selectedBody = null, Func<DalamudLinkPayload,bool>? routeLinkOwner = null)
    {
        this.chat=chat; this.framework=framework; this.log=log; this.history=history; this.enabled=enabled;
        this.classify=classify; this.selected=selected; this.format=format; this.selectedBody=selectedBody; this.routeLinkOwner=routeLinkOwner ?? (link=>TeleportRoute.OwnsLink(chat,link));
        try { chat.ChatMessageUnhandled += OnMessage; Subscribed=true; }
        catch (Exception ex)
        {
            Cleanup.Run(error=>log.Warning(error,"Game log subscription rollback failed"),()=>chat.ChatMessageUnhandled-=OnMessage);
            log.Warning(ex,"Game log copy unavailable");
        }
    }
    private void OnMessage(IChatMessage message)
    {
        if(disposed || !Subscribed || !framework.IsInFrameworkUpdateThread) return;
        try
        {
            if(!enabled() || message.IsHandled) return;
            var kind=(int)message.LogKind; var source=(int)message.SourceKind; var target=(int)message.TargetKind; var timestamp=message.Timestamp;
            if(!selected(kind,source,target) || classify()!=ChatCallOrigin.Game) return;
            var ownedMessage=message.Message;
            var isPlayer=PositionMessages.IsPlayerKind(kind);
            var relatedRoute=isPlayer ? history.RouteOf(ownedMessage) : 0;
            var sender=message.Sender.Encode(); var body=ownedMessage.Encode();
            // A copied player statement contains its original map/text, not a plugin route.
            if(isPlayer && PositionMessages.HasMapLink(body))
            {
                var original=message.OriginalMessage.AsSpan().Data.ToArray();
                if(TeleportRoute.TryRestore(kind,original,body,routeLinkOwner,out var restored)) body=restored.Encode();
            }
            if(sender.Length+body.Length>History.MaxRecordBytes || selectedBody?.Invoke(kind,source,target,body)==false) return;
            var formatted=format(kind,sender,body);
            var plain=Dalamud.Game.Text.SeStringHandling.SeString.Parse(formatted).TextValue;
            if(history.TryAdd(id=>new(id,kind,source,target,timestamp,[],formatted,plain) { Origin=ChatRecordOrigin.Game, RelatedRouteId=relatedRoute },out _)) Copied++;
        }
        catch(Exception ex) { Failures++; if(Failures<=3) log.Warning(ex,"Game log copy failed; original game output is preserved"); }
    }
    internal static string Diagnostic(bool enabled, bool catalogAvailable, string catalogDiagnostic,
        bool captureCompatible, string captureDiagnostic, bool drawingFault, bool subscribed, bool hasSelection)
    {
        if(drawingFault) return "停止中: 描画エラーが発生しています。再ロードしてください。";
        if(!catalogAvailable) return "利用不可: "+catalogDiagnostic;
        if(!captureCompatible) return "利用不可: 取り込み元を判定できません。"+captureDiagnostic;
        if(!subscribed) return "利用不可: チャットイベントを購読できませんでした。再ロードしてください。";
        if(!enabled) return "OFF（設定で無効）";
        if(!hasSelection) return "待機中: 有効なタブの表示項目が選択されていません。";
        return "有効: 選択したゲームログをコピーします。";
    }
    public void Dispose()
    {
        if(disposed) return;
        disposed=true; Subscribed=false;
        Cleanup.Run(ex=>log.Warning(ex,"Game event cleanup failed"),()=>chat.ChatMessageUnhandled-=OnMessage);
    }
}
