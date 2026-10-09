using PluginChatOverlay;

internal static class CleanupChecks
{
    public static void Run()
    {
        var released = new List<string>(); var errors = new List<Exception>();
        Cleanup.Run(errors.Add,
            () => released.Add("events"),
            () => throw new IOException("tooltip release failed"),
            () => released.Add("font"),
            () => released.Add("command"));
        Check(released.SequenceEqual(["events", "font", "command"]) && errors.Count == 1,
            "failed tooltip cleanup does not prevent remaining fonts and command from releasing");
        Cleanup.Run(_ => throw new Exception("logger failed"),
            () => throw new Exception("first release failed"),
            () => released.Add("last"));
        Check(released.Last() == "last", "failed cleanup reporting still allows remaining cleanup");
    }
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
}
