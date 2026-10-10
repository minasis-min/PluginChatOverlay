namespace PluginChatOverlay;

/// <summary>Retain pending native cleanup until it succeeds; never poll when idle.</summary>
internal sealed class TooltipCleanupRetry(Func<bool> cleanup, Action<Action, TimeSpan> schedule, Action<Exception> report)
{
    private readonly object gate = new();
    private bool pending, queued, reported;
    private int attempts;
    public bool Pending { get { lock (gate) return pending; } }
    public void Request(bool immediate = false)
    {
        lock (gate)
        {
            if (queued) return;
            if (!pending) { attempts = 0; reported = false; }
            pending = queued = true;
        }
        if (immediate) Attempt(); else Schedule(TimeSpan.Zero);
    }
    private void Attempt()
    {
        TimeSpan delay;
        lock (gate)
        {
            queued = false;
            if (!pending) return;
            try
            {
                if (cleanup()) { pending = false; attempts = 0; return; }
            }
            catch (Exception ex) { if (!reported) { reported = true; Report(ex); } }
            attempts = Math.Min(attempts + 1, 6);
            queued = true;
            delay = TimeSpan.FromMilliseconds(Math.Min(250 * (1 << Math.Min(attempts - 1, 5)), 5000));
        }
        Schedule(delay);
    }
    private void Schedule(TimeSpan delay)
    {
        try { schedule(Attempt, delay); }
        catch (Exception ex)
        {
            // Preserve pending ownership; a later Request can enqueue again.
            lock (gate) queued = false;
            Report(ex);
        }
    }
    private void Report(Exception ex) { try { report(ex); } catch { } }
}
