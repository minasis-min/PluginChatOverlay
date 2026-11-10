using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
namespace PluginChatOverlay;
internal sealed class QuestToastCapture : IDisposable
{
    private readonly IToastGui toast;
    private readonly Func<bool> ready, onThread, queueCall;
    private readonly Func<byte[], QuestToastPosition, uint, bool> accept;
    private bool disposed, subscribed;
    public string Diagnostic { get; private set; } = "未確認";
    public long Captured { get; private set; }
    public long Passed { get; private set; }
    public long Failures { get; private set; }
    public QuestToastCapture(IToastGui toast, Func<bool> onThread, Func<bool> ready,
        Func<byte[], QuestToastPosition, uint, bool> accept, Func<bool>? queueCall = null)
    {
        this.toast = toast; this.ready = ready; this.onThread = onThread; this.accept = accept;
        this.queueCall = static () => false;
        try
        {
            this.queueCall = queueCall ?? ToastOrigin.Create().IsQueueCall;
            toast.QuestToast += OnQuest; subscribed = true; Diagnostic = "対応するプラグイン通知キューを確認済み";
        }
        catch (Exception ex)
        {
            try { toast.QuestToast -= OnQuest; } catch { }
            Diagnostic = "通常表示を維持: " + ex.GetType().Name + " " + ex.Message;
        }
    }
    private void OnQuest(ref SeString message, ref QuestToastOptions options, ref bool handled)
    {
        if (disposed || !subscribed || handled || !onThread() || !ready()) return;
        try
        {
            // Other formats/sound/checkmarks keep their native appearance and behavior.
            if (options.Position != QuestToastPosition.Centre || options.DisplayCheckmark || options.PlaySound || !queueCall()) { Passed++; return; }
            var bytes = message.Encode();
            if (bytes.Length is 0 or > 8192 || !accept(bytes, options.Position, options.IconId)) { Passed++; return; }
            handled = true; Captured++;
        }
        catch { Failures++; }
    }
    public void Suspend(string reason) { Diagnostic = "通常表示を維持: " + reason; Dispose(); }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (subscribed) { subscribed = false; toast.QuestToast -= OnQuest; }
    }
}
internal sealed class ToastQueue(Func<long> clock)
{
    internal sealed record Banner(byte[] Bytes, QuestToastPosition Position, uint IconId, long Began, long Expires);
    private readonly List<Banner> active = [];
    internal const int DurationMs = 3500;
    internal const int Capacity = 8;
    private void RemoveExpired(long now) => active.RemoveAll(banner => banner.Expires <= now);
    public bool TryAdd(byte[] bytes, QuestToastPosition position, uint iconId)
    {
        if (bytes.Length is 0 or > 8192 || !Enum.IsDefined(position)) return false;
        var now = clock(); RemoveExpired(now);
        // Never replace an accepted notification. Overflow remains native.
        if (active.Count >= Capacity) return false;
        active.Add(new(bytes.ToArray(), position, iconId, now, now + DurationMs)); return true;
    }
    public Banner[] Snapshot()
    {
        RemoveExpired(clock());
        return active.ToArray();
    }
    public void Clear() => active.Clear();
}
