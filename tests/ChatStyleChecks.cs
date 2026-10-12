using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using MinasisUI;
using PluginChatOverlay;

internal static class ChatStyleChecks
{
    internal static unsafe void Run()
    {
        var context = ImGui.CreateContext();
        try
        {
            var io = ImGui.GetIO(); io.IniFilename = null; io.LogFilename = null;
            io.DisplaySize = new(1200, 900); io.DeltaTime = 1f / 60;
            io.Fonts.AddFontDefault(); byte* pixels; int width, height;
            io.Fonts.GetTexDataAsRGBA32(0, &pixels, &width, &height);
            ImGui.NewFrame(); ImGui.Begin("Custom style checks");
            using var ui = new Ui();
            foreach (var fromMinasis in new[] { false, true })
            {
                var snapshot = new ChatStyleSnapshot();
                using (fromMinasis ? new OverlayThemeScope(ui) : null) snapshot.Capture(1);
                var settings = new Settings { AppearanceMode = ChatAppearanceMode.Custom, CustomChatStyle = snapshot,
                    LineSpacing = -4, CustomMessageGap = 6, InnerPaddingX = 15, CustomWindowLayout = true };
                var restored = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings))!;
                restored.Normalize();
                Check(restored.CustomChatStyle!.IsValid && restored.CustomChatStyle.Colors.SequenceEqual(snapshot.Colors)
                    && restored.CustomChatStyle.Metrics.SequenceEqual(snapshot.Metrics) && restored.LineSpacing == -4
                    && restored.CustomMessageGap == 6 && restored.InnerPaddingX == 15, "custom base and edited values survive settings round trip");
                foreach (var scale in new[] { 1f, 1.5f, 2f })
                using (fromMinasis ? null : new OverlayThemeScope(ui))
                {
                    var selected = new ChatStyleSnapshot(); selected.Capture(scale);
                    var popup = ChatPopupStyle.Capture();
                    try
                    {
                        using var custom = snapshot.Push(scale);
                        Check(ImGui.GetStyle().WindowPadding == new Vector2(snapshot.Metrics[0], snapshot.Metrics[1]) * scale
                            && ImGui.GetStyle().ScrollbarSize == snapshot.Metrics[20] * scale
                            && ImGui.GetStyle().Colors[(int)ImGuiCol.Tab] == Color(snapshot, ImGuiCol.Tab)
                            && ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBg] == Color(snapshot, ImGuiCol.FrameBg),
                            $"custom geometry, tab and control colors stay fixed across UI switching at {scale}");
                        using (new ChatPopupStyle.Scope(popup, true))
                            Check(ImGui.GetStyle().Colors[(int)ImGuiCol.Tab] == Color(popup.FullStyle!, ImGuiCol.Tab)
                                && ImGui.GetStyle().FramePadding.X == popup.FullStyle!.Metrics[2] * popup.Scale,
                                "popup restores the selected UI beyond the chat frame colors");
                        throw new InvalidOperationException("scope restoration check");
                    }
                    catch (InvalidOperationException) { }
                    var after = new ChatStyleSnapshot(); after.Capture(scale);
                    Check(after.Colors.SequenceEqual(selected.Colors) && after.Metrics.SequenceEqual(selected.Metrics),
                        "custom base restores every captured host value after an exception");
                }
                restored.CustomMessageGap = 12;
                Check(restored.LineSpacing == -4 && restored.ResolveMessageGap(99, 1.5f) == 18,
                    "message-gap editing leaves the previous line-height adjustment intact");
                restored.LineSpacing = 3;
                Check(restored.CustomMessageGap == 12, "line-height editing leaves message spacing intact");
                restored.ResetLayout();
                Check(restored.CustomChatStyle == null && restored.CustomMessageGap == null, "full reset clears the captured custom base");
            }
            var malformed = new Settings { CustomChatStyle = new() { Colors = [float.NaN], Metrics = [] } };
            malformed.Normalize(); Check(malformed.CustomChatStyle == null, "malformed custom style is discarded safely");
            var reusable = new ChatStyleSnapshot(); reusable.Capture(1);
            for (var i = 0; i < 100; i++) reusable.Capture(1);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++) reusable.Capture(1);
            Check(GC.GetAllocatedBytesForCurrentThread() == before, "reused per-frame popup style capture allocates no managed memory");
            ImGui.End(); ImGui.Render();
        }
        finally { ImGui.DestroyContext(context); }
    }
    private static Vector4 Color(ChatStyleSnapshot snapshot, ImGuiCol color)
    { var i = (int)color * 4; return new(snapshot.Colors[i], snapshot.Colors[i + 1], snapshot.Colors[i + 2], snapshot.Colors[i + 3]); }
    private static void Check(bool value, string name)
    { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
}
