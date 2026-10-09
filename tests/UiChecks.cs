using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using MinasisUI;
using PluginChatOverlay;

internal static class UiChecks
{
    public static unsafe void Run()
    {
        Check(new Settings().UiStyle == OverlayUiStyle.Dalamud && JsonSerializer.Deserialize<Settings>("{\"FontSize\":23}")!.UiStyle == OverlayUiStyle.Dalamud,
            "new and existing settings default to Dalamud UI");
        var selected = new Settings { UiStyle = OverlayUiStyle.Minasis, FontSize = 24, ShowTabs = false };
        var restored = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(selected))!;
        restored.Normalize();
        Check(restored.UiStyle == OverlayUiStyle.Minasis && restored.FontSize == 24 && !restored.ShowTabs, "Minasis UI preference persists with existing font and tabs");
        restored.UiStyle = (OverlayUiStyle)99; restored.Normalize();
        Check(restored.UiStyle == OverlayUiStyle.Dalamud, "invalid UI preference safely returns to Dalamud");
        var context = ImGui.CreateContext();
        try
        {
            var io = ImGui.GetIO(); io.IniFilename = null; io.LogFilename = null;
            io.DisplaySize = new(1600, 1200); io.DeltaTime = 1f / 60;
            io.Fonts.AddFontDefault();
            byte* pixels; int w, h; io.Fonts.GetTexDataAsRGBA32(0, &pixels, &w, &h);
            using var ui = new Ui(); using var other = new Ui { Theme = UiTheme.Cream };
            var flag = true; var integer = 17; var number = 80f; var text = "Tab 1"; var index = 1;
            UiChoice[] choices = [new("native", "Dalamud"), new("minasis", "MinasisUI")];
            foreach (var scale in new[] { 1f, 1.5f, 2f })
            {
                ui.Scale = scale;
                var padding = ImGui.GetStyle().WindowPadding; var color = ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
                var tabColor = ImGui.GetStyle().Colors[(int)ImGuiCol.TabActive];
                ImGui.NewFrame();
                using (new OverlayThemeScope(ui))
                {
                    ImGui.SetNextWindowSize(new(900, 950));
                    ImGui.Begin("Settings###PCO-check");
                    try
                    {
                        if (ImGui.BeginTabBar("SettingsTabs"))
                        {
                            try
                            {
                                if (ImGui.BeginTabItem("Basic"))
                                {
                                    try
                                    {
                                        ui.Heading("Appearance"); ui.Dropdown("ui", "UI", ref index, choices);
                                        ui.Checkbox("enabled", "Capture", ref flag);
                                        ui.InputInt("font", "Font px", ref integer);
                                        ui.Slider("alpha", "Background", ref number, 0, 100, "%.0f");
                                        ui.InputText("name", "Tab name", ref text, 48); ui.Button("apply", "Apply");
                                    }
                                    finally { ImGui.EndTabItem(); }
                                }
                            }
                            finally { ImGui.EndTabBar(); }
                        }
                    }
                    finally { ImGui.End(); }
                }
                ImGui.Render();
                Check(ImGui.GetStyle().WindowPadding == padding && ImGui.GetStyle().Colors[(int)ImGuiCol.Text] == color
                    && ImGui.GetStyle().Colors[(int)ImGuiCol.TabActive] == tabColor && ImGui.GetDrawData().TotalVtxCount > 0,
                    $"Minasis native controls render at {scale} scale and restore host text, tabs and dimensions");
                ImGui.NewFrame();
                var left = Vector2.Zero; var right = Vector2.Zero; var fontRestored = false;
                using (new OverlayThemeScope(ui))
                {
                    ImGui.SetNextWindowSize(new(740, 540)); ui.ConstrainNextTwoColumnWindow();
                    ImGui.Begin("Sidebar###PCO-sidebar-check");
                    try
                    {
                        ui.TwoColumn("layout", () =>
                        {
                            left = ImGui.GetWindowPos(); var font = ImGui.GetFont();
                            ui.NavigationItem("basic", "Basic", true, "*", font);
                            ui.NavigationItem("history", "History", false, "+", font);
                            fontRestored = ImGui.GetFont().Handle == font.Handle;
                        }, () => { right = ImGui.GetWindowPos(); ui.Heading("Settings"); });
                    }
                    finally { ImGui.End(); }
                }
                ImGui.Render();
                Check(right.X > left.X && Math.Abs(right.Y - left.Y) < 1 && fontRestored
                    && ImGui.GetStyle().WindowPadding == padding,
                    $"icon sidebar stays left at {scale} scale and restores host font and layout");
            }
            ImGui.NewFrame();
            var before = ImGui.GetStyle().WindowPadding;
            try { using var scope = new OverlayThemeScope(ui); throw new IOException("consumer failure"); }
            catch (IOException) { }
            Check(ImGui.GetStyle().WindowPadding == before, "Minasis overlay theme unwinds on consumer exception");
            var duplicate = new OverlayThemeScope(ui); duplicate.Dispose(); duplicate.Dispose();
            Check(ImGui.GetStyle().WindowPadding == before, "Minasis overlay theme tolerates repeated disposal");
            ImGui.EndFrame();
            ui.Dispose();
            Check(other.Theme == UiTheme.Cream, "disposing embedded UI preserves other instance state");
        }
        finally { ImGui.DestroyContext(context); }
    }
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
}
