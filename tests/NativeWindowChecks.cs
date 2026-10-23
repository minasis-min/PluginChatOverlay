using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using PluginChatOverlay;

internal static class NativeWindowChecks
{
    public static void Run()
    {
        foreach (var uiScale in new[] { 1f, 1.5f, 2f })
        {
            var anchor = new WindowRect(100, 80, 24 * uiScale, 24 * uiScale);
            var origin = new System.Numerics.Vector2(300, 200);
            var layout = NativeButtonLayout.Create(anchor, origin, 1, 18, uiScale);
            Check(layout.Position.X == origin.X + anchor.X + anchor.Width + 8 * uiScale
                && layout.Height == anchor.Height && layout.FontSize == 18 * uiScale,
                $"native shortcut at {uiScale} scale clears scaled anchor and matches native button height");
            var shifted = NativeButtonLayout.Create(anchor, origin, 1, 18, uiScale, new(10, -7));
            Check(shifted.Position == layout.Position + new System.Numerics.Vector2(10, -7)
                && shifted.Height == layout.Height && shifted.FontSize == layout.FontSize,
                $"native offsets remain physical pixels at {uiScale} scale without changing button size");
        }
        var offsets = new Settings { NativeButtonOffsetX = -24, NativeButtonOffsetY = 8 };
        var offsetSaved = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(offsets))!;
        offsetSaved.Normalize();
        Check(offsetSaved.NativeButtonOffsetX == -24 && offsetSaved.NativeButtonOffsetY == 8
            && JsonSerializer.Deserialize<Settings>("{}")!.NativeButtonOffsetX == 0,
            "native offsets persist and legacy settings retain the automatic position");
        offsets.NativeButtonOffsetX = int.MinValue; offsets.NativeButtonOffsetY = int.MaxValue; offsets.Normalize();
        Check(offsets.NativeButtonOffsetX == -2000 && offsets.NativeButtonOffsetY == 2000,
            "native offsets normalize out-of-range settings safely");
        var savedFont = JsonSerializer.Deserialize<Settings>("{\"NativeButtonFontSize\":14}")!;
        savedFont.Normalize();
        Check(savedFont.NativeButtonFontSize == 14 && new Settings().NativeButtonFontSize == 18,
            "native shortcut retains saved text size while new default is larger");
        var settings = new Settings { Tabs = [
            new() { Id = "main-tab", Detached = false },
            new() { Id = "a", Detached = true, Visible = true },
            new() { Id = "b", Detached = true, Visible = true },
            new() { Id = "child", ParentWindowId = "a", Detached = false }
        ] };
        Check(settings.ToggleWindowVisibility("main") && settings.Visible && !settings.IsWindowVisible("main")
            && settings.IsWindowVisible("a") && settings.IsWindowVisible("b"), "native main toggle leaves detached windows visible");
        Check(settings.ToggleWindowVisibility("a") && !settings.IsWindowVisible("a") && settings.IsWindowVisible("b"),
            "native detached toggle leaves other windows unchanged");
        settings.Visible = false;
        Check(settings.ToggleWindowVisibility("a") && settings.IsWindowVisible("a")
            && !settings.IsWindowVisible("b") && !settings.IsWindowVisible("main"),
            "native restore from global-hidden opens only chosen detached window");
        Check(settings.ToggleWindowVisibility("main") && settings.IsWindowVisible("main") && settings.IsWindowVisible("a")
            && !settings.IsWindowVisible("b"), "native main restore preserves each detached visibility");
        settings.Visible = false;
        Check(settings.ToggleWindowVisibility("main") && settings.IsWindowVisible("main")
            && !settings.IsWindowVisible("a") && !settings.IsWindowVisible("b"),
            "native restore from global-hidden opens only main window");
        settings.Visible = false;
        var unchanged = JsonSerializer.Serialize(settings);
        Check(!settings.ToggleWindowVisibility("missing") && !settings.ToggleWindowVisibility("child")
            && JsonSerializer.Serialize(settings) == unchanged, "invalid native choices cannot enable global visibility or toggle child tabs");
        var legacy = JsonSerializer.Deserialize<Settings>("{\"Visible\":false}")!;
        legacy.Normalize();
        Check(!legacy.Visible && legacy.MainWindowVisible, "legacy global-hidden settings retain hidden state and default main visibility");
        settings.Visible = true; settings.MainWindowVisible = false;
        settings.Tabs.Single(t => t.Id == "a").Visible = true;
        var restored = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings))!; restored.Normalize();
        Check(!restored.MainWindowVisible && restored.IsWindowVisible("a") && !restored.IsWindowVisible("b"),
            "main and detached visibility persist independently through settings normalization");
        restored.Attach(restored.Tabs.Single(t => t.Id == "b"), "main");
        Check(restored.MainWindowVisible && restored.IsWindowVisible("main"), "attaching a tab to hidden main restores destination visibility");

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var plugin = (Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
        typeof(Plugin).GetField("settings", flags)!.SetValue(plugin, settings);
        typeof(Plugin).GetField("gameLogCatalog", flags)!.SetValue(plugin, new GameLogCatalog([]));
        var chatWindowType = typeof(Plugin).GetNestedType("ChatWindow", BindingFlags.NonPublic)!;
        foreach (var detached in new[] { false, true })
        {
            var tab = detached ? new ChatTab { Id = "external-chat", Detached = true } : null;
            var window = (Dalamud.Interface.Windowing.Window)Activator.CreateInstance(chatWindowType,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, [plugin, tab?.Id ?? "main", tab], null)!;
            Check(!window.ForceMainWindow,
                $"{(detached ? "detached" : "main")} chat window follows Dalamud multi-monitor policy without a game-window lock");
        }
        var directory = Path.Combine(AppContext.BaseDirectory, "native-window-checks", Guid.NewGuid().ToString("N"));
        typeof(Plugin).GetField("store", flags)!.SetValue(plugin, SettingsStore.ForDistribution(directory, AppContext.BaseDirectory));
        settings.MainWindowVisible = false;
        typeof(Plugin).GetMethod("OpenMain", flags)!.Invoke(plugin, null);
        Check(settings.MainWindowVisible && settings.IsWindowVisible("main"), "show command restores individually hidden primary main window");
        typeof(Plugin).GetMethod("ToggleNativeWindow", flags)!.Invoke(plugin, ["main"]);
        var saved = JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine(directory, "settings.json")))!;
        Check(!saved.MainWindowVisible && saved.Visible && saved.IsWindowVisible("a"),
            "native button integration saves main visibility without hiding detached window");
    }
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
}
