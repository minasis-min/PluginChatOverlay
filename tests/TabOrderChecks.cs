using System.Text.Json;
using Dalamud.Bindings.ImGui;
using PluginChatOverlay;
internal static class TabOrderChecks
{
    private static void Check(bool condition,string label) { if(!condition) throw new Exception(label); Console.WriteLine("PASS "+label); }
    public static void Run()
    {
        var root=new ChatTab { Id="root", Name="Root", Detached=true, X=321, Width=876 };
        var other=new ChatTab { Id="other", Name="Other", Detached=true };
        var child=new ChatTab { Id="child", Name="Child", ParentWindowId="root", GameLogs=new(){ShowMapMessages=true} };
        var last=new ChatTab { Id="last", Name="Last", ParentWindowId="root" };
        var settings=new Settings { Tabs=[root,other,child,last] };
        Check(settings.MoveTab("child",-1) && settings.WindowTabs("root").Select(t=>t.Id).SequenceEqual(["child","root","last"]),"tab move changes only sibling order across interleaved windows");
        Check(settings.Tabs.Where(t=>t.Detached).SequenceEqual([root,other]),"tab ordering cannot change the primary window or other root-window order");
        Check(settings.WindowTabs("other").Single()==other && settings.WindowOf(child)=="root" && root.Detached && root.X==321 && root.Width==876 && child.GameLogs.ShowMapMessages,"tab move preserves windows, geometry, filters and stable identity");
        Check(!settings.MoveTab("child",-1) && !settings.MoveTab("last",1) && !settings.MoveTab("missing",1) && !settings.MoveTab("root",0),"tab-order boundaries and unknown IDs leave settings unchanged");
        var restored=SettingsStore.Parse(JsonSerializer.Serialize(settings));
        Check(restored.WindowTabs("root").Select(t=>t.Id).SequenceEqual(["child","root","last"]) && restored.Tabs.Single(t=>t.Id=="child").GameLogs.ShowMapMessages,"saved tab order survives settings reload without losing map preferences");
        var uiSettings=new Settings { Tabs=[new(){Id="a",Name="A",Detached=true},new(){Id="b",Name="B",ParentWindowId="a"}] };
        var positions=new Dictionary<string,float>();
        var reset=false;
        void Draw()
        {
            for(var frame=0;frame<3;frame++)
            {
                ImGui.NewFrame(); ImGui.SetNextWindowPos(new(0,0)); ImGui.SetNextWindowSize(new(800,300));
                ImGui.Begin("PCO tab order regression");
                try
                {
                    if(ImGui.BeginTabBar("reorder-check"))
                    {
                        try
                        {
                            if(reset) { foreach(var tab in uiSettings.WindowTabs("a")) ImGui.SetTabItemClosed("###reorder-"+tab.Id); reset=false; }
                            foreach(var tab in uiSettings.WindowTabs("a"))
                            {
                                var active=ImGui.BeginTabItem(tab.Name+"###reorder-"+tab.Id);
                                positions[tab.Id]=ImGui.GetItemRectMin().X;
                                if(active) ImGui.EndTabItem();
                            }
                        }
                        finally { ImGui.EndTabBar(); }
                    }
                }
                finally { ImGui.End(); }
                ImGui.Render();
            }
        }
        Draw(); Check(positions["a"]<positions["b"],"native tab bar initially follows configured sibling order");
        uiSettings.MoveTab("b",-1); reset=true; Draw();
        Check(positions["b"]<positions["a"],"native tab bar updates the actual header positions after saved order changes");
    }
}
