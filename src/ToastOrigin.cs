using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Plugin.Services;
namespace PluginChatOverlay;

// Positive managed queue proof. Missing/inlined/reentrant frames pass through.
internal sealed class ToastOrigin
{
    private readonly MethodBase detour, show, update;
    private static readonly Dictionary<string, string> Contracts = new()
    {
        ["UpdateQueue"] = "E08AB3E8452821453EA9A7B2908E47CF580A55432E29B4ADA07D7FFCD60652C1",
        ["ShowQuest"] = "16861B22828ACF21439BCCEA43782D1637FFFF6BA46174AD59DD33F089EF2909",
        ["HandleQuestToastDetour"] = "D710708ED12ACC4ED342293EEEE5DAF1255E2C3F6A627F30498D7B3874473B2C",
    };
    internal ToastOrigin(MethodBase detour, MethodBase show, MethodBase update) { this.detour = detour; this.show = show; this.update = update; }
    public static ToastOrigin Create()
    {
        var type = typeof(IToastGui).Assembly.GetType("Dalamud.Game.Gui.Toast.ToastGui", true)!;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var update = type.GetMethod("UpdateQueue", flags)!;
        var show = type.GetMethods(flags).Single(m => m.Name == "ShowQuest" && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(byte[]));
        var detour = type.GetMethod("HandleQuestToastDetour", flags)!;
        foreach (var method in new[] { update, show, detour })
            if (method == null || MethodCompatibility.Fingerprint(method) != Contracts[method.Name]) throw new NotSupportedException("トースト経路が未対応です。" + method?.Name);
        return new(detour, show, update);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool IsQueueCall() => Matches(new StackTrace(false).GetFrames());
    internal bool Matches(IReadOnlyList<MethodBase?> frames) => Match(frames, static f => f);
    private bool Matches(StackFrame[] frames) => Match(frames, static f => f.GetMethod());
    private bool Match<T>(IReadOnlyList<T> frames, Func<T, MethodBase?> method)
    {
        var index = -1;
        for (var i = 0; i < frames.Count; i++)
        {
            if (!Equals(method(frames[i]), detour)) continue;
            if (index >= 0) return false;
            index = i;
        }
        return index >= 0 && index + 2 < frames.Count && Equals(method(frames[index + 1]), show) && Equals(method(frames[index + 2]), update);
    }
}
