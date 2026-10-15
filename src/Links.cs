using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace PluginChatOverlay;

internal sealed class Links(IChatGui chat, IGameGui game, IPluginLog log, IFramework framework, Func<long>? clock = null)
{
    private readonly NativeItemTooltip native = new(game, log, framework);
    private TooltipHover? hover;
    private TooltipHover Hover => hover ??= new(native);
    public string TooltipStatus => native.Status;
    public void BeginTooltipFrame() => Hover.BeginFrame();
    public void EndTooltipFrame() => Hover.EndFrame();
    public void CloseTooltip() => Hover.Close();
    public void InitializeTooltips(IAddonLifecycle lifecycle, IGameConfig config) => native.Register(lifecycle, config);
    public void DisposeTooltips() { try { CloseTooltip(); } finally { native.Unregister(); } }
    public void SetTooltipWindows(WindowRect[] windows, System.Numerics.Vector2 origin, System.Numerics.Vector2 size) => native.SetWindows(windows, origin, size);
    private readonly Func<long> now = clock ?? (() => Environment.TickCount64);
    private string notice = "";
    private long noticeUntil;
    public string Notice
    {
        get
        {
            if (notice.Length > 0 && now() >= noticeUntil) notice = "";
            return notice;
        }
        private set { notice = value; noticeUntil = value.Length == 0 ? 0 : now() + 5000; }
    }
    public void DrawTooltip(byte[] bytes, int offset)
    {
        try
        {
            if (Envelope(bytes, offset).Payloads.FirstOrDefault() is ItemPayload item)
                Hover.Request(TooltipItem.From(item));
        }
        catch { /* Stale or unsupported game data must not interrupt chat rendering. */ }
    }
    public void Activate(byte[] bytes, int offset)
    {
        SeString? linkText = null;
        try
        {
            if (offset < 0 || offset >= bytes.Length) return;
            linkText = Envelope(bytes, offset);
            switch (linkText.Payloads.FirstOrDefault())
            {
                case DalamudLinkPayload link:
                    // Look up now; never cache delegates from disabled/unloaded plugins.
                    if (chat.RegisteredLinkHandlers.TryGetValue((link.Plugin, link.CommandId), out var handler))
                    {
                        handler(link.CommandId, linkText);
                        Notice = "";
                    }
                    else Report(linkText, $"リンクは利用できません: {link.Plugin} / {link.CommandId}");
                    break;
                case MapLinkPayload map:
                    if (game.OpenMapWithMapLink(map)) Notice = "";
                    else Report(linkText, "マップを開けませんでした。");
                    break;
                default:
                    Report(linkText, "このリンクのクリック操作は未対応です。アイテムの詳細はホバーで表示できます。");
                    break;
            }
        }
        catch (Exception ex) { Report(linkText, "リンク処理に失敗しました。詳細はDalamudログを確認してください。"); log.Error(ex, "Overlay link failed"); }
    }
    private void Report(SeString? link, string reason)
    {
        string description;
        try { description = Describe(link); }
        catch { description = "ゲームリンク（名前を取得できません）"; }
        Notice = description + ": " + reason;
        // Normal chat routing applies: separation ON sends this plain-text notice to PCO.
        // Never re-emit the clickable payload, and never report on per-frame hover retries.
        try { chat.Print(new SeString(new TextPayload("[PluginChatOverlay] " + Notice))); }
        catch (Exception ex) { log.Warning(ex, "Unable to print link action notice"); }
    }
    internal static string Describe(SeString? link)
    {
        var kind = link?.Payloads.FirstOrDefault() switch
        {
            ItemPayload => "アイテム",
            PlayerPayload => "プレイヤー",
            MapLinkPayload => "マップ",
            DalamudLinkPayload => "プラグインリンク",
            _ => "ゲームリンク",
        };
        var label = new string((link?.TextValue ?? "").Where(c => !char.IsControl(c)).Take(160).ToArray()).Trim();
        return label.Length == 0 ? kind + "（名前を取得できません）" : kind + "「" + label + "」";
    }
    internal static SeString Envelope(byte[] bytes, int offset)
    {
        var parsed = SeString.Parse(bytes.AsSpan(offset));
        var result = new SeString();
        var terminator = RawPayload.LinkTerminator.Encode();
        var ended = false;
        foreach (var payload in parsed.Payloads)
        {
            result.Payloads.Add(payload);
            if (!payload.Encode().AsSpan().SequenceEqual(terminator)) continue;
            ended = true; break;
        }
        if (!ended) throw new FormatException("Link terminator missing");
        return result;
    }
}
