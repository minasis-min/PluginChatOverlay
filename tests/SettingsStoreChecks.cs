using PluginChatOverlay;

internal static class SettingsStoreChecks
{
    public static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "PCO-config-check-" + Guid.NewGuid().ToString("N"));
        if (!string.Equals(Path.GetPathRoot(root), "D:\\", StringComparison.OrdinalIgnoreCase)) throw new IOException("Storage checks must run on D");
        var assemblyDir = Path.Combine(root, "assembly");
        var configDir = Path.Combine(root, "standard-config");
        var legacy = new SettingsStore(assemblyDir);
        legacy.Save(new Settings { ShowTabs = false, FontSize = 24, Tabs = [new() { Id = "root", Detached = true, Width = 820, Height = 430 }, new() { Id = "child", ParentWindowId = "root" }] });
        var distribution = SettingsStore.ForDistribution(configDir, assemblyDir);
        var imported = distribution.Load();
        Check(imported.FontSize == 24 && !imported.ShowTabs && imported.WindowTabs("root").Count() == 2
            && File.Exists(Path.Combine(configDir, "settings.json")) && File.Exists(Path.Combine(assemblyDir, "data", "settings.json")),
            "distribution config migration preserves existing settings and leaves D development config intact");
        imported.FontSize = 19; distribution.Save(imported);
        legacy.Save(new Settings { FontSize = 30 });
        Check(distribution.Load().FontSize == 19 && !File.Exists(Path.Combine(configDir, "settings.json.tmp")),
            "standard config takes precedence and atomic save completes without leftover temporary file");
        var rejected = false;
        try { SettingsStore.ForDistribution("relative-config", assemblyDir); } catch (IOException) { rejected = true; }
        Check(rejected, "distribution settings reject relative storage path");
        var standardFile = Path.Combine(configDir, "settings.json");
        const string damaged = "{\"Tabs\": [ unfinished";
        File.WriteAllText(standardFile, damaged);
        var loadFailed = false; var saveRejected = false;
        try { distribution.Load(); } catch (System.Text.Json.JsonException) { loadFailed = true; }
        try { distribution.Save(new Settings()); } catch (IOException) { saveRejected = true; }
        Check(loadFailed && saveRejected && File.ReadAllText(standardFile) == damaged,
            "failed settings load blocks default save and preserves original bytes");
        File.WriteAllText(standardFile, "null");
        loadFailed = saveRejected = false;
        try { distribution.Load(); } catch (System.Text.Json.JsonException) { loadFailed = true; }
        try { distribution.Save(new Settings()); } catch (IOException) { saveRejected = true; }
        Check(loadFailed && saveRejected && File.ReadAllText(standardFile) == "null",
            "JSON null is rejected and remains protected from default overwrite");
        File.WriteAllText(standardFile, "{\"FontSize\":24,\"ShowTabs\":false}");
        var repaired = distribution.Load(); repaired.FontSize = 22; distribution.Save(repaired);
        Check(distribution.Load().FontSize == 22 && !distribution.Load().ShowTabs,
            "successful reload of repaired settings restores saving and retains preferences");
        var failedConfig = Path.Combine(root, "failed-migration");
        Directory.CreateDirectory(Path.Combine(failedConfig, "settings.json.tmp"));
        var failing = SettingsStore.ForDistribution(failedConfig, assemblyDir);
        var migrationFailed = false; var migrationBlocked = false;
        try { failing.Load(); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { migrationFailed = true; }
        try { failing.Save(new Settings()); } catch (IOException) { migrationBlocked = true; }
        Check(migrationFailed && migrationBlocked && legacy.Load().FontSize == 30,
            "failed migration blocks fallback saves and preserves valid legacy settings");
        // Keep generated verification files under D TEMP; no machine/game settings are touched.
    }
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
}
