using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace PluginChatOverlay;

/// <summary>Positive proof of the immediate managed queue path, never a content heuristic.</summary>
internal sealed class QueueOrigin(MethodBase detour, MethodBase update)
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool IsQueueCall() => Matches(new StackTrace(false).GetFrames().Select(f => f.GetMethod()).ToArray());
    public bool IsNonRecursiveCall() => new StackTrace(false).GetFrames().Count(f => Equals(f.GetMethod(), detour)) == 1;

    public bool Matches(IReadOnlyList<MethodBase?> frames)
    {
        var index = -1;
        for (var i = 0; i < frames.Count; i++)
        {
            if (!Equals(frames[i], detour)) continue;
            // ChatGui reuses one mutable event object. Reject recursive native/event calls entirely.
            if (index >= 0) return false;
            index = i;
        }
        return index >= 0 && index + 1 < frames.Count && Equals(frames[index + 1], update);
    }
}
