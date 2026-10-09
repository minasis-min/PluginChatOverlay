using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace PluginChatOverlay;

internal sealed class Links(IChatGui chat, IGameGui game, IPluginLog log)
{
    private readonly NativeItemTooltip native = new(game, log);
    private TooltipHover? hover;
    private TooltipHover Hover => hover ??= new(native);
    public string TooltipStatus => native.Status;
    public void BeginTooltipFrame() => Hover.BeginFrame();
    public void EndTooltipFrame() => Hover.EndFrame();
    public void CloseTooltip() => Hover.Close();
    public void InitializeTooltips(IAddonLifecycle lifecycle, IGameConfig config) => native.Register(lifecycle, config);
    public void DisposeTooltips() { try { CloseTooltip(); } finally { native.Unregister(); } }
    public void SetTooltipWindows(WindowRect[] windows, System.Numerics.Vector2 origin, System.Numerics.Vector2 size) => native.SetWindows(windows, origin, size);
    public string Notice { get; private set; } = "";
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
        try
        {
            if (offset < 0 || offset >= bytes.Length) return;
            var linkText = Envelope(bytes, offset);
            switch (linkText.Payloads.FirstOrDefault())
            {
                case DalamudLinkPayload link:
                    // Look up now; never cache delegates from disabled/unloaded plugins.
                    if (chat.RegisteredLinkHandlers.TryGetValue((link.Plugin, link.CommandId), out var handler))
                    {
                        handler(link.CommandId, linkText);
                        Notice = "";
                    }
                    else Notice = $"リンクは利用できません: {link.Plugin} / {link.CommandId}";
                    break;
                case MapLinkPayload map:
                    Notice = game.OpenMapWithMapLink(map) ? "" : "マップを開けませんでした。";
                    break;
                default:
                    Notice = "このゲームリンクの操作は初版では未対応です（アイテム・プレイヤー・URL等）。";
                    break;
            }
        }
        catch (Exception ex) { Notice = "リンク処理に失敗しました。詳細はDalamudログを確認してください。"; log.Error(ex, "Overlay link failed"); }
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
