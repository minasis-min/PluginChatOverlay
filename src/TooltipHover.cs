using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;

namespace PluginChatOverlay;

internal readonly record struct TooltipItem(uint RawId, ItemKind Kind)
{
    public static TooltipItem From(ItemPayload item) => new(item.RawItemId, item.Kind);
}
internal interface IItemTooltipBackend
{
    bool Show(TooltipItem item);
    void Hide(TooltipItem item);
    bool IsOpen(TooltipItem item);
}
internal sealed class TooltipHover(IItemTooltipBackend backend)
{
    private TooltipItem? requested, opened;
    public void BeginFrame() => requested = null;
    public void Request(TooltipItem item) => requested = item;
    public void EndFrame()
    {
        if (requested == opened && (opened is not { } current || backend.IsOpen(current))) return;
        Close();
        if (requested is { } item && backend.Show(item)) opened = item;
    }
    public void Close()
    {
        if (opened is not { } item) return;
        opened = null;
        backend.Hide(item);
    }
}
