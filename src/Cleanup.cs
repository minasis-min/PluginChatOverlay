namespace PluginChatOverlay;

internal static class Cleanup
{
    public static void Run(Action<Exception> report, params Action[] actions)
    {
        foreach (var action in actions)
        {
            try { action(); }
            catch (Exception ex)
            {
                try { report(ex); }
                catch { /* Reporting must not prevent the remaining resources from being released. */ }
            }
        }
    }
}
