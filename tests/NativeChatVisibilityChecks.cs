using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Runtime.InteropServices;
using Dalamud.Interface.Windowing;
using Mono.Cecil;
using PluginChatOverlay;

internal static class NativeChatVisibilityChecks
{
    public static unsafe void Run()
    {
        Check(!new Settings().HideWhenNativeChatExpanded
            && !JsonSerializer.Deserialize<Settings>("{}")!.HideWhenNativeChatExpanded,
            "native chat expansion hiding is opt-in for new and legacy settings");
        var saved = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(new Settings { HideWhenNativeChatExpanded = true }))!;
        saved.Normalize();
        Check(saved.HideWhenNativeChatExpanded, "native chat expansion hiding survives save and normalization");
        Span<byte> byteRead = stackalloc byte[1];
        Check(!SafeProcessMemory.Read(0, byteRead) && !SafeProcessMemory.Read(1, byteRead),
            "invalid native memory returns failure without dereferencing it");
        Check(!NativeChatVisibility.MatchesCode(new byte[181]) && !NativeChatVisibility.MatchesCode([]),
            "unknown or incomplete game code cannot enable expansion flag reads");
        var memory = Marshal.AllocHGlobal(0x638);
        try
        {
            new Span<byte>((void*)memory, 0x638).Clear();
            Marshal.WriteByte(memory, 0x5E8, 1);
            Check(!NativeChatVisibility.ReadExpanded(memory, 0x5E8), "an invisible or initializing chat cannot hide the overlay");
            Marshal.WriteInt32(memory, 0x198, 1 << 21);
            Check(NativeChatVisibility.ReadExpanded(memory, 0x5E8), "visible native main chat reads the inspected expansion flag without calling game code");
            Marshal.WriteByte(memory, 0x5E8, 0);
            Marshal.WriteByte(memory, 0x3A0, 1);
            Check(!NativeChatVisibility.ReadExpanded(memory, 0x5E8) && NativeChatVisibility.ReadExpanded(memory, 0x3A0),
                "native panel maximization and main restoration use independent flags");
            Marshal.WriteByte(memory, 0x3A0, 2);
            Check(!NativeChatVisibility.ReadExpanded(memory, 0x3A0)
                && Marshal.ReadInt32(memory, 0x198) == (1 << 21) && Marshal.ReadByte(memory, 0x3A0) == 2,
                "unknown flag values remain visible and the probe never changes native memory");
        }
        finally { Marshal.FreeHGlobal(memory); }
        using (var assembly = AssemblyDefinition.ReadAssembly(typeof(Plugin).Assembly.Location))
        {
            Check(!assembly.MainModule.Types.SelectMany(t => t.Methods).Where(m => m.HasBody)
                .SelectMany(m => m.Body.Instructions).Any(i => i.Operand is MethodReference method && method.Name == "IsZoomed"),
                "compiled plugin has no native IsZoomed call even with persisted expansion hiding enabled");
            var pluginType = assembly.MainModule.Types.Single(t => t.Name == nameof(Plugin));
            var shortcuts = pluginType.Methods.Where(m => m.Name is "DrawNativeChatButtons" or "DrawNativeButtonRow" or "DrawNativeIcon" or "DrawNativeShortcut");
            Check(!shortcuts.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions)
                .Any(i => i.Operand is MethodReference method && method.Name is "PushFont" or "PopFont" or "SetWindowFontScale"),
                "native shortcut draw path cannot mutate shared font stack or window font scale");
            var probe = assembly.MainModule.Types.Single(t => t.Name == nameof(NativeChatVisibility));
            Check(!probe.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions)
                .Any(i => i.OpCode == Mono.Cecil.Cil.OpCodes.Calli), "expansion probe never invokes a game function pointer");
        }

        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        var settings = new Settings { HideWhenNativeChatExpanded = true, HideLoggedOut = false, HideDuringCutscene = false };
        var plugin = (Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
        typeof(Plugin).GetField("settings", fields)!.SetValue(plugin, settings);
        var views = typeof(Plugin).GetField("views", fields)!;
        views.SetValue(plugin, Activator.CreateInstance(views.FieldType));
        var hidden = typeof(Plugin).GetField("chatHiddenByNativeExpansion", fields)!;
        var windowType = typeof(Plugin).GetNestedType("ChatWindow", BindingFlags.NonPublic)!;
        var windows = new[] { "main", "detached" }.Select(id =>
        {
            var window = (Window)RuntimeHelpers.GetUninitializedObject(windowType);
            windowType.GetField("owner", fields)!.SetValue(window, plugin);
            windowType.GetField("id", fields)!.SetValue(window, id);
            return window;
        }).ToArray();
        var original = JsonSerializer.Serialize(settings);
        hidden.SetValue(plugin, true);
        Check(windows.All(w => !w.DrawConditions()), "native chat expansion hides main and detached window draw conditions");
        hidden.SetValue(plugin, false);
        Check(windows.All(w => w.DrawConditions()) && JsonSerializer.Serialize(settings) == original,
            "closing native expansion restores drawing without rewriting saved window visibility");
    }
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
}
