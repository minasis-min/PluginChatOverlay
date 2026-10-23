using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace PluginChatOverlay;

internal enum ChatCallOrigin { Unknown, Plugin, Game }

/// <summary>Positive proof of the immediate managed queue path, never a content heuristic.</summary>
internal sealed class QueueOrigin(MethodBase detour, MethodBase update)
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool IsQueueCall() => ClassifyFrames(new StackTrace(false).GetFrames(), static f => f.GetMethod()) == ChatCallOrigin.Plugin;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool IsNonRecursiveCall() => FindDetour(new StackTrace(false).GetFrames(), static f => f.GetMethod()) >= 0;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public ChatCallOrigin ClassifyCurrentCall() => ClassifyFrames(new StackTrace(false).GetFrames(), static f => f.GetMethod());

    public ChatCallOrigin Classify(IReadOnlyList<MethodBase?> frames) => ClassifyFrames(frames, static f => f);
    public bool Matches(IReadOnlyList<MethodBase?> frames) => Classify(frames) == ChatCallOrigin.Plugin;

    private ChatCallOrigin ClassifyFrames<T>(IReadOnlyList<T> frames, Func<T, MethodBase?> method)
    {
        var index = FindDetour(frames, method);
        if (index < 0) return ChatCallOrigin.Unknown;
        return index + 1 < frames.Count && Equals(method(frames[index + 1]), update) ? ChatCallOrigin.Plugin : ChatCallOrigin.Game;
    }
    private int FindDetour<T>(IReadOnlyList<T> frames, Func<T, MethodBase?> method)
    {
        var index = -1;
        for (var i = 0; i < frames.Count; i++)
        {
            if (!Equals(method(frames[i]), detour)) continue;
            // ChatGui reuses one mutable event object. Reject recursive native/event calls entirely.
            if (index >= 0) return -1;
            index = i;
        }
        return index;
    }
}
