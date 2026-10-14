using System.Reflection;

namespace PluginChatOverlay;

internal static class ChatCompatibility
{
    internal const string QueueFingerprint = "2F8A8817481E74DD354EC5A5DE12C57C35E5D43D722BB517D20F3A40BCF8FB8F";
    internal const string DetourFingerprint = "1CB21AC76FA5C0DB030A3590022AB05082248ACB1319BF392A9828EF0BB2C60E";
    internal static bool Matches(MethodBase detour, MethodBase update) =>
        MatchesFingerprints(MethodCompatibility.Fingerprint(detour), MethodCompatibility.Fingerprint(update));
    internal static bool MatchesFingerprints(string detour, string queue) =>
        detour == DetourFingerprint && queue == QueueFingerprint;
    internal static string Diagnose(string detour, string queue)
    {
        var changed = new List<string>();
        if (detour != DetourFingerprint) changed.Add("HandlePrintMessageDetour（通知処理）");
        if (queue != QueueFingerprint) changed.Add("UpdateQueue（プラグイン通知の出力経路）");
        return changed.Count == 0 ? "互換性あり" : "非互換: " + string.Join("、", changed) + " が変更されています。通常チャットへ通します。";
    }
}
