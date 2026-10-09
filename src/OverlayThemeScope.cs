using Dalamud.Bindings.ImGui;
using MinasisUI;

namespace PluginChatOverlay;

// Include tabs in the selected palette without changing the pinned kit sources.
internal sealed class OverlayThemeScope : IDisposable
{
    private readonly StyleScope theme;
    private int colors;
    public OverlayThemeScope(Ui ui)
    {
        theme = ui.PushTheme();
        try
        {
            ImGui.PushStyleColor(ImGuiCol.Tab, ui.Theme.Inset); colors++;
            ImGui.PushStyleColor(ImGuiCol.TabHovered, ui.Theme.Hover); colors++;
            ImGui.PushStyleColor(ImGuiCol.TabActive, ui.Theme.Selected); colors++;
            ImGui.PushStyleColor(ImGuiCol.TabUnfocused, ui.Theme.Inset); colors++;
            ImGui.PushStyleColor(ImGuiCol.TabUnfocusedActive, ui.Theme.Selected); colors++;
        }
        catch { Dispose(); throw; }
    }
    public void Dispose()
    {
        if (colors > 0) { ImGui.PopStyleColor(colors); colors = 0; }
        theme.Dispose();
    }
}
