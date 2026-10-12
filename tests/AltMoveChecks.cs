using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using PluginChatOverlay;

internal static class AltMoveChecks
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    public static unsafe void Run()
    {
        Check(new Settings().AltDragMove && SettingsStore.Parse("{}").AltDragMove,
            "Alt drag is available by default for new and legacy settings");
        Check(!SettingsStore.Parse(JsonSerializer.Serialize(new Settings { AltDragMove = false })).AltDragMove,
            "disabling Alt drag persists without changing appearance migration");
        var context = ImGui.CreateContext();
        try
        {
            var io = ImGui.GetIO(); io.IniFilename = null; io.LogFilename = null;
            io.DisplaySize = new(1600, 1200); io.DeltaTime = 1f / 60;
            io.Fonts.AddFontDefault();
            byte* pixels; int width, height; io.Fonts.GetTexDataAsRGBA32(0, &pixels, &width, &height);
            foreach (var detached in new[] { false, true })
            foreach (var pass in new[] { false, true })
            foreach (var restriction in new[] { "none", "pinned", "no-move", "disabled" })
            {
                var settings = new Settings { ShowWindowTitle = false, ShowTabs = false, ClickThrough = pass,
                    SnapWindows = false, Pinned = restriction == "pinned", AllowMove = restriction != "no-move", AltDragMove = restriction != "disabled" };
                var plugin = (Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
                Set(plugin, "settings", settings); Set(plugin, "copyInput", new CopyInput());
                var views = typeof(Plugin).GetField("views", Fields)!;
                views.SetValue(plugin, Activator.CreateInstance(views.FieldType));
                var tab = detached ? new ChatTab { Id = "detached", ClickThrough = pass, Pinned = settings.Pinned } : null;
                var id = tab?.Id ?? "main";
                var windowType = typeof(Plugin).GetNestedType("ChatWindow", BindingFlags.NonPublic)!;
                var window = (Window)Activator.CreateInstance(windowType, Fields, null, [plugin, id, tab], null)!;
                var view = typeof(Plugin).GetMethod("View", Fields)!.Invoke(plugin, [id])!;
                Set(view, "Host", window); Set(view, "ResetGeometry", false);
                var drag = typeof(Plugin).GetMethod("DragHeader", Fields)!;
                Vector2 moved = default;
                string? saved = null;
                var name = $"Alt-{detached}-{pass}-{restriction}";
                for (var frame = 0; frame < 7; frame++)
                {
                    io.AddKeyEvent(ImGuiKey.ModAlt, frame is >= 1 and <= 3);
                    io.AddMouseButtonEvent(0, frame is >= 2 and <= 5);
                    io.AddMousePosEvent(frame < 3 ? 140 : frame == 3 ? 180 : 230, frame < 3 ? 140 : frame == 3 ? 170 : 200);
                    ImGui.NewFrame();
                    ImGui.SetNextWindowPos(new(100, 100), ImGuiCond.Once);
                    ImGui.SetNextWindowSize(new(300, 180));
                    window.PreDraw();
                    try
                    {
                        Check(window.IsClickthrough == (pass && !(settings.AllowMove && settings.AltDragMove && !settings.Pinned && io.KeyAlt)),
                            $"{name} temporary interaction honors modifier and movement restrictions at frame {frame}");
                        ImGui.Begin(name, window.Flags | (window.IsClickthrough ? ImGuiWindowFlags.NoInputs : ImGuiWindowFlags.None));
                        try
                        {
                            drag.Invoke(plugin, [id, false]);
                            ImGui.Dummy(new(250, 110));
                            if (frame == 3)
                            {
                                moved = ImGui.GetWindowPos();
                                Check(moved == (restriction == "none" ? new Vector2(140, 130) : new Vector2(100, 100)),
                                    $"{name} titleless and tabless body drag respects restrictions");
                            }
                            if (frame is 4 or 5)
                                Check(ImGui.GetWindowPos() == moved && !(bool)Get(view, "HeaderDragging")!,
                                    $"{name} releasing Alt stops motion while the mouse remains held");
                        }
                        finally { ImGui.End(); }
                    }
                    finally { window.PostDraw(); }
                    saved ??= JsonSerializer.Serialize(settings);
                    Check(window.IsClickthrough == pass && JsonSerializer.Serialize(settings) == saved,
                        $"{name} temporary interaction never overwrites saved clickthrough or other preferences");
                    ImGui.Render();
                }
            }
            io.ConfigInputTrickleEventQueue = false;
            foreach (var detached in new[] { false, true })
            foreach (var pass in new[] { false, true })
            foreach (var mouseFirst in new[] { false, true })
                CheckSimultaneousGesture(detached, pass, mouseFirst, false);
            CheckSimultaneousGesture(false, true, false, true);
            CheckSimultaneousGesture(true, true, true, true);
            CheckSimultaneousGesture(false, true, false, true, true);
            CheckSimultaneousGesture(true, true, true, true, true);
        }
        finally { ImGui.DestroyContext(context); }
    }
    private static void CheckSimultaneousGesture(bool detached, bool pass, bool mouseFirst, bool blocked, bool opensDuringPending = false)
    {
        var io = ImGui.GetIO();
        var settings = new Settings { ShowWindowTitle = false, ShowTabs = false, ClickThrough = pass, SnapWindows = true };
        var plugin = (Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
        Set(plugin, "settings", settings); Set(plugin, "copyInput", new CopyInput());
        var views = typeof(Plugin).GetField("views", Fields)!;
        views.SetValue(plugin, Activator.CreateInstance(views.FieldType));
        var tab = detached ? new ChatTab { Id = "detached", ClickThrough = pass } : null;
        var id = tab?.Id ?? "main";
        var windowType = typeof(Plugin).GetNestedType("ChatWindow", BindingFlags.NonPublic)!;
        var window = (Window)Activator.CreateInstance(windowType, Fields, null, [plugin, id, tab], null)!;
        var view = typeof(Plugin).GetMethod("View", Fields)!.Invoke(plugin, [id])!;
        Set(view, "Host", window); Set(view, "ResetGeometry", false);
        var drag = typeof(Plugin).GetMethod("DragHeader", Fields)!;
        var name = $"Simultaneous-{detached}-{pass}-{mouseFirst}-{blocked}-{opensDuringPending}";
        for (var frame = 0; frame < 6; frame++)
        {
            io.AddKeyEvent(ImGuiKey.ModAlt, frame is 2 or 3 || mouseFirst && frame == 4);
            io.AddMouseButtonEvent(0, frame is 2 or 3 || !mouseFirst && frame == 4);
            io.AddMousePosEvent(frame < 3 ? 140 : frame == 3 || mouseFirst && frame == 4 ? 48 : 230, 140);
            ImGui.NewFrame();
            if (opensDuringPending && frame >= 3) DrawCoverWindow(name, frame == 3);
            ImGui.SetNextWindowPos(new(100, 100), ImGuiCond.Once); ImGui.SetNextWindowSize(new(300, 180));
            window.PreDraw();
            try
            {
                ImGui.Begin(name, window.Flags | (window.IsClickthrough ? ImGuiWindowFlags.NoInputs : ImGuiWindowFlags.None));
                try
                {
                    drag.Invoke(plugin, [id, false]);
                    if (opensDuringPending && frame == 2)
                        Check((int)Get(view, "PendingAltFrame")! == ImGui.GetFrameCount(),
                            $"{name} regression starts with an actual pending Alt press before the new UI opens");
                    var pos = ImGui.GetWindowPos(); var size = ImGui.GetWindowSize();
                    Set(view, "Window", new WindowRect(pos.X, pos.Y, size.X, size.Y));
                    ImGui.Dummy(new(250, 110));
                    if (frame == 3)
                        Check(pos.X == (blocked ? 100 : 8), $"{name} same-frame Alt/click starts a fast drag or respects covering UI");
                    if (frame >= 4)
                        Check(pos.X == (blocked ? 100 : 0) && !(bool)Get(view, "HeaderDragging")!,
                            $"{name} both release orders finish at the snapped edge without following later mouse motion");
                }
                finally { ImGui.End(); }
            }
            finally { window.PostDraw(); }
            Check(window.IsClickthrough == pass && settings.ClickThrough == pass,
                $"{name} simultaneous input and snapping retain saved clickthrough");
            if (blocked && !opensDuringPending) DrawCoverWindow(name, false);
            ImGui.Render();
        }
    }
    private static void DrawCoverWindow(string name, bool newlyOpened)
    {
        ImGui.SetNextWindowPos(new(110, 110), ImGuiCond.Always); ImGui.SetNextWindowSize(new(200, 120));
        ImGui.Begin("Cover-" + name, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoTitleBar);
        try
        {
            if (newlyOpened)
            {
                var window = ImGuiP.GetCurrentWindowRead();
                Check(window.Active && !window.WasActive, $"{name} covering window is active only in the current frame");
            }
            ImGui.Dummy(new(150, 80));
        }
        finally { ImGui.End(); }
    }
    private static object? Get(object target, string field) => target.GetType().GetField(field, Fields)!.GetValue(target);
    private static void Set(object target, string field, object? value) => target.GetType().GetField(field, Fields)!.SetValue(target, value);
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
}
