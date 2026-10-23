using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Bindings.ImGui;
using MinasisUI;
using PluginChatOverlay;

internal static class GameLogUiChecks
{
    public static void Run()
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var plugin=(Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
        var settings=new Settings {CopyGameLogs=true};
        var catalog=new GameLogCatalog(Enumerable.Range(1,14).Where(c=>c!=3).SelectMany(c=>Enumerable.Range(1,16)
            .Select(i=>new GameLogFilterEntry((uint)(c*100+i),"日本語のログ項目 "+i,41,2,ushort.MaxValue,c,i))));
        var tab=new ChatTab {GameLogs=new(){Enabled=true,Rows=[101,401]}};
        using var ui=new Ui();
        typeof(Plugin).GetField("settings",flags)!.SetValue(plugin,settings);
        typeof(Plugin).GetField("gameLogCatalog",flags)!.SetValue(plugin,catalog);
        typeof(Plugin).GetField("ui",flags)!.SetValue(plugin,ui);
        var draw=typeof(Plugin).GetMethod("DrawGameLogCategory",flags)!;
        var drawAll=typeof(Plugin).GetMethod("DrawGameLogFilters",flags)!;
        foreach(var minasis in new[]{false,true}) foreach(var scale in new[]{1f,1.5f,2f})
        {
            typeof(Plugin).GetField("minasisFrame",flags)!.SetValue(plugin,minasis); ui.Scale=scale;
            foreach(var category in Enumerable.Range(1,14).Where(c=>c!=3))
            {
                ImGui.NewFrame();
                var font=ImGui.GetFont(); var padding=ImGui.GetStyle().WindowPadding;
                using(minasis ? new OverlayThemeScope(ui) : null)
                {
                    ImGui.SetNextWindowSize(new(720,1000)); ImGui.Begin("Native filter UI checks");
                    try { draw.Invoke(plugin,[tab.GameLogs,category]); }
                    finally { ImGui.End(); }
                }
                if(!ImGui.GetFont().Equals(font) || ImGui.GetStyle().WindowPadding!=padding) throw new Exception("native filter UI leaked font/style");
                ImGui.Render();
            }
            foreach(var enabled in new[]{false,true})
            {
                tab.GameLogs.Enabled=enabled; ImGui.NewFrame();
                using(minasis ? new OverlayThemeScope(ui) : null)
                {
                    ImGui.SetNextWindowSize(new(720,1000)); ImGui.Begin("Native category tabs");
                    try { drawAll.Invoke(plugin,[tab]); }
                    finally { ImGui.End(); }
                }
                ImGui.Render();
            }
            if(!tab.GameLogs.Rows.SetEquals([101u,401u])) throw new Exception("UI switching changed filter values");
            BulkActionClicks(plugin, ui, minasis, tab.GameLogs, catalog.Entries.Length);
            Console.WriteLine($"PASS all native filter categories render under {(minasis ? "Minasis" : "Dalamud")} at {scale} scale, restore font/style, and retain selections");
        }
    }
    private static void BulkActionClicks(Plugin plugin, Ui ui, bool minasis, GameLogFilter filter, int total)
    {
        var draw = typeof(Plugin).GetMethod("DrawGameLogBulkActions", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var io = ImGui.GetIO();
        var on = new System.Numerics.Vector2(-1000); var off = on;
        var offChanged = false; var onChanged = false;
        for (var frame = 0; frame < 8; frame++)
        {
            var target = frame == 0 ? new System.Numerics.Vector2(-1000) : frame < 4 ? off : on;
            io.AddMousePosEvent(target.X, target.Y); io.AddMouseButtonEvent(0, frame is 2 or 5);
            ImGui.NewFrame();
            using (minasis ? new OverlayThemeScope(ui) : null)
            {
                ImGui.SetNextWindowPos(new(20, 20)); ImGui.SetNextWindowSize(new(720, 300));
                ImGui.Begin("Native bulk button clicks", ImGuiWindowFlags.NoMove);
                try
                {
                    on = ImGui.GetCursorScreenPos() + new System.Numerics.Vector2(10, 10);
                    var changed = (bool)draw.Invoke(plugin, [filter])!;
                    off = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
                    if (frame == 3) offChanged = changed && filter.Rows.Count == 0;
                    if (frame == 6) onChanged = changed && filter.Rows.Count == total;
                }
                finally { ImGui.End(); }
            }
            ImGui.Render();
        }
        io.AddMousePosEvent(-1000, -1000); io.AddMouseButtonEvent(0, false);
        filter.Rows = [101, 401];
        if (!offChanged || !onChanged) throw new Exception("native bulk ON/OFF click failed: " + minasis + "/" + ui.Scale);
        Console.WriteLine($"PASS native bulk ON/OFF actual clicks under {(minasis ? "Minasis" : "Dalamud")} at {ui.Scale} scale");
    }
}
