using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Bindings.ImGui;
using MinasisUI.Workspace;
using PluginChatOverlay;
internal static class WorkspaceChecks
{
    public static void Run()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var create = typeof(Plugin).GetMethod("CreateWorkspace", BindingFlags.Static | BindingFlags.NonPublic)!;
        using var workspace = (WorkspaceUi)create.Invoke(null, [null])!;
        var plugin = (Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
        var settings = new Settings();
        typeof(Plugin).GetField("workspace", flags)!.SetValue(plugin, workspace);
        typeof(Plugin).GetField("ui", flags)!.SetValue(plugin, workspace.Controls);
        typeof(Plugin).GetField("settings", flags)!.SetValue(plugin, settings);
        typeof(Plugin).GetField("minasisFrame", flags)!.SetValue(plugin, true);
        var draw = typeof(Plugin).GetMethod("DrawPluginToastSettings", flags)!;
        foreach (var minasis in new[] { false, true }) foreach (var scale in new[] { 1f, 1.5f, 2f }) foreach (var width in new[] { 450, 600, 740, 1200 })
        {
            typeof(Plugin).GetField("minasisFrame", flags)!.SetValue(plugin, minasis);
            ImGui.NewFrame(); var font = ImGui.GetFont(); var padding = ImGui.GetStyle().WindowPadding;
            workspace.SetFonts(font, font, font, font, font, font, scale);
            workspace.State.ExpandedSize = new(width, 700);
            using (workspace.PushChrome())
            {
                ImGui.SetNextWindowPos(new(0, 0)); ImGui.SetNextWindowSize(new(width, 700));
                ImGui.Begin("PCO workspace regression", ImGuiWindowFlags.NoTitleBar);
                try
                {
                    workspace.Select("layout", "links");
                    workspace.Draw(() => workspace.PageScrollPane("test-body", () => draw.Invoke(plugin, null)));
                    if (workspace.Tab != 3 || workspace.Page != 2) throw new Exception("workspace selection changed");
                }
                finally { ImGui.End(); }
            }
            workspace.ClearFonts(); ImGui.Render();
            if (ImGui.GetFont() != font || ImGui.GetStyle().WindowPadding != padding || settings.SeparatePluginToasts)
                throw new Exception("workspace leaked state or changed the opt-in");
            Console.WriteLine($"PASS PCO notification card in {(minasis ? "Minasis" : "Dalamud")} at width {width}, scale {scale} restores drawing state and retains values");
        }
        var tabPages=new[]{"manage","plugins","game","window"};
        for(var i=0;i<tabPages.Length;i++)
        {
            workspace.Select("tabs",tabPages[i]);
            if(workspace.Page!=1 || workspace.Tab!=i) throw new Exception("tab settings page is missing: "+tabPages[i]);
        }
        Console.WriteLine("PASS tab settings have four pages with ordering included in the window page");
        workspace.Select("layout", "toasts");
        if (workspace.Page != 2 || workspace.Tab != 4) throw new Exception("toast layout tab is missing");
        workspace.Select("layout", "reset");
        if (workspace.Tab != 5) throw new Exception("toast tab replaced the reset page");
        Console.WriteLine("PASS toast presentation has a dedicated layout tab while reset remains reachable");
        ImGui.NewFrame(); ImGui.Begin("Workspace root size regression");
        workspace.State.ExpandedSize = new(900, 630);
        ImGui.BeginChild("smaller settings content", new(656, 574));
        typeof(Plugin).GetMethod("UpdateWorkspaceConfigSize", flags)!.Invoke(plugin, null);
        ImGui.EndChild(); ImGui.End(); ImGui.Render();
        if (settings.ConfigWidth != 900 || settings.ConfigHeight != 630) throw new Exception("persisted child size instead of root size");
        Console.WriteLine("PASS settings persist the root workspace size from within a smaller content pane");
        workspace.IsOpen = false; workspace.IsOpen = true;
        if (workspace.Page != 0 || workspace.Tab != 0) throw new Exception("reopen did not reset navigation");
        Console.WriteLine("PASS reopening shared settings returns to the first category without changing plugin settings");
    }
}
