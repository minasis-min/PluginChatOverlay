namespace PluginChatOverlay;

/// <summary>Keep the consumed chord suppressed through focus loss until physical release.</summary>
internal sealed class CopyInput
{
    private bool consumed;
    private readonly object gate = new();
    private bool request;
    private object? requestContext;
    public bool Suppress(bool focused, bool control, bool cDown, object? context = null)
    {
        lock (gate)
        {
            if (!control || !cDown) { consumed = false; return false; }
            if (focused && !consumed) { consumed = true; requestContext = context; request = true; }
            return consumed;
        }
    }
    public bool TakeRequest() => TakeRequest(out _);
    public bool TakeRequest(out object? context)
    {
        lock (gate)
        {
            context = requestContext; requestContext = null;
            var result = request; request = false; return result;
        }
    }
    public void CancelRequest() { lock (gate) { request = false; requestContext = null; } }
    public void Reset() { lock (gate) { consumed = false; request = false; requestContext = null; } }
}
