using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Plugin.Services;
using PluginChatOverlay;

internal static class CopyShortcutChecks
{
    public static void Run()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var plugin = (Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
        void Set(string field, object value) => typeof(Plugin).GetField(field, flags)!.SetValue(plugin, value);
        var preferences = new Settings { CopyWithCtrlC = false };
        var input = new CopyInput(); input.Suppress(true, true, true, new object());
        var access = 0; var writes = 0;
        var keys = DispatchProxy.Create<IKeyState, ServiceProxy>();
        ((ServiceProxy)(object)keys).Result = (method, _) =>
        {
            access++; if (method.Name == "set_Item") writes++;
            return method.Name == "get_Item" ? false : null;
        };
        Set("settings", preferences); Set("copyInput", input); Set("keyState", keys);
        Set("history", new History()); Set("renderCacheGate", new object());
        Set("chatFocused", true); Set("controlDown", true); Set("copyKeyDown", true);
        Set("inputUpdatedAt", Environment.TickCount64);
        typeof(Plugin).GetMethod("SuppressGameCopy", flags)!.Invoke(plugin, [DispatchProxy.Create<IFramework, ServiceProxy>()]);
        Check(access == 0 && !input.TakeRequest(), "disabled Ctrl+C bypasses all shared game-key reads and writes even with stale focused chord");
        input.Suppress(true, true, true, new object());
        typeof(Plugin).GetMethod("CompleteCopyRequest", flags)!.Invoke(plugin, null);
        Check(!input.TakeRequest() && (bool)typeof(Plugin).GetField("chatFocused", flags)!.GetValue(plugin)!,
            "disabled Ctrl+C discards queued copy without touching ImGui focus or clipboard");
        typeof(Plugin).GetMethod("CaptureDrawCopyShortcut", flags)!.Invoke(plugin, [null, false]);
        Check(access == 0 && !input.TakeRequest(),
            "disabled draw-side Ctrl+C also returns before accessing game keys or ImGui chord state");
        preferences.CopyWithCtrlC = true;
        typeof(Plugin).GetMethod("SuppressGameCopy", flags)!.Invoke(plugin, [DispatchProxy.Create<IFramework, ServiceProxy>()]);
        Check(writes == 1 && input.TakeRequest(), "reenabled Ctrl+C restores overlay copy request and consumed-key behavior");
    }
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
}
