using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Bindings.ImGui;
using MinasisUI;
using PluginChatOverlay;
internal static class HoverTextChecks
{
    public static void Run()
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var method=typeof(Plugin).GetMethod("UiTooltip",flags,null,[typeof(Action),typeof(Vector2?)],null)!;
        foreach(var minasis in new[]{false,true}) foreach(var scale in new[]{1f,1.5f,2f})
        {
            var plugin=(Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
            var ui=new Ui { Scale=scale };
            typeof(Plugin).GetField("ui",flags)!.SetValue(plugin,ui);
            typeof(Plugin).GetField("minasisFrame",flags)!.SetValue(plugin,minasis);
            var io=ImGui.GetIO(); var target=new Vector2(-1000); var observed=0; var alphaFound=false;
            Vector2 shortSize=default,longSize=default;
            foreach(var longText in new[]{false,true})
            for(var frame=0;frame<8;frame++)
            {
                io.AddMousePosEvent(target.X,target.Y); ImGui.NewFrame();
                using var theme=minasis ? new OverlayThemeScope(ui) : null;
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(80,40));
                ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize,new Vector2(700,500));
                ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize,7);
                ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize,9);
                ImGui.PushStyleVar(ImGuiStyleVar.Alpha,.4f);
                ImGui.PushStyleColor(ImGuiCol.PopupBg,new Vector4(.12f,.23f,.34f,1));
                try
                {
                    ImGui.SetNextWindowPos(new(20,20)); ImGui.SetNextWindowSize(new(1000,700));
                    ImGui.Begin("PCO hover text regression",ImGuiWindowFlags.NoMove|ImGuiWindowFlags.NoSavedSettings);
                    try
                    {
                        ImGui.InvisibleButton("hover-target",new(120,30)); target=(ImGui.GetItemRectMin()+ImGui.GetItemRectMax())/2;
                        var font=ImGui.GetFont();
                        Action content=()=>
                        {
                            observed++;
                            var style=ImGui.GetStyle();
                            if(style.WindowPadding!=new Vector2(14,10)*scale || style.PopupBorderSize!=0 || style.WindowBorderSize!=0 || style.WindowMinSize!=Vector2.Zero || style.Alpha!=1)
                                throw new Exception("tooltip did not override inherited geometry/alpha");
                            var text=longText ? string.Join(" ",Enumerable.Repeat("Long tooltip description",80)) : "Short";
                            ImGui.TextUnformatted(text);
                            if(frame==7)
                            {
                                if(longText) longSize=ImGui.GetWindowSize(); else shortSize=ImGui.GetWindowSize();
                                var background=ImGui.ColorConvertFloat4ToU32(new(.12f,.23f,.34f,1))&0x00FFFFFFu;
                                var draw=ImGui.GetWindowDrawList();
                                for(var i=0;i<draw.VtxBuffer.Size;i++)
                                    if((draw.VtxBuffer[i].Col&0x00FFFFFFu)==background && (draw.VtxBuffer[i].Col>>24)==204) alphaFound=true;
                            }
                        };
                        method.Invoke(plugin,[content,null]);
                        if(ImGui.GetFont()!=font || ImGui.GetStyle().WindowPadding!=new Vector2(80,40) || ImGui.GetStyle().Alpha!=.4f || ImGui.GetStyle().PopupBorderSize!=9)
                            throw new Exception("tooltip leaked drawing state");
                    }
                    finally { ImGui.End(); }
                }
                finally { ImGui.PopStyleColor(); ImGui.PopStyleVar(5); }
                ImGui.Render();
            }
            if(observed==0 || !alphaFound || shortSize.X>=700 || shortSize.Y>=500 || longSize.Y<=shortSize.Y || longSize.X>ImGui.GetFontSize()*24+28*scale+4)
                throw new Exception($"tooltip sizing/opacity/wrapping failed: {observed}, {alphaFound}, {shortSize}, {longSize}");
            Console.WriteLine($"PASS hover text under {(minasis ? "Minasis" : "Dalamud")} scale {scale}: 80% background, no border, scaled 14x10 padding, auto-size/wrap and state restore");
            io.AddMousePosEvent(-1000,-1000);
        }
    }
}
