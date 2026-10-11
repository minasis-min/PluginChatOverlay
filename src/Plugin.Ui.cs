using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using MinasisUI;

namespace PluginChatOverlay;

public sealed partial class Plugin
{
    private readonly Ui ui = new();
    private UiHeadingFonts? headingFonts;
    private bool minasisFrame;
    private ChatPopupStyle popupStyle;
    private uint originalChatColor;
    private static readonly UiChoice[] UiStyles = [new("dalamud", "Dalamud標準"), new("minasis", "MinasisUI")];

    // Capture once per frame so changing the selector cannot mix control styles or unbalance stacks.
    private OverlayThemeScope? BeginUiTheme()
    {
        minasisFrame = settings.UiStyle == OverlayUiStyle.Minasis;
        originalChatColor = ImGui.GetColorU32(ImGuiCol.Text);
        if (!minasisFrame) { popupStyle = ChatPopupStyle.Capture(); return null; }
        ui.Theme = UiTheme.SoftCharcoal;
        ui.Scale = ImGuiHelpers.GlobalScale;
        var scope = new OverlayThemeScope(ui);
        popupStyle = ChatPopupStyle.Capture();
        return scope;
    }
    private bool DrawUiStyleSettings(bool sidebar = false)
    {
        if (!sidebar) UiSection("UIの外観");
        var selected = (int)settings.UiStyle;
        var changed = false;
        ImGui.SetNextItemWidth(sidebar ? ImGui.GetContentRegionAvail().X : 240 * ImGuiHelpers.GlobalScale);
        if (minasisFrame) changed = ui.Dropdown("ui-style", "UI", ref selected, UiStyles);
        else if (ImGui.BeginCombo("UI", UiStyles[selected].Label))
        {
            try
            {
                for (var i = 0; i < UiStyles.Length; i++)
                    if (ImGui.Selectable(UiStyles[i].Label, selected == i)) { selected = i; changed = true; }
            }
            finally { ImGui.EndCombo(); }
        }
        if (changed)
        {
            settings.UiStyle = (OverlayUiStyle)selected;
            // Remeasure line heights even when the wrap width happens to stay unchanged.
            InvalidateRowLayouts();
        }
        if (!sidebar)
        {
            ImGui.TextWrapped("チャットと設定の外観を切り替えます。本文の色と文字サイズはそのままです。既定はDalamud標準です。");
            UiSection("チャットの表示");
        }
        return changed;
    }
    private void UiSection(string label)
    {
        if (minasisFrame) ui.Heading(label);
        else { ImGui.Spacing(); ImGui.Separator(); ImGui.TextUnformatted(label); ImGui.Spacing(); }
    }
    private bool UiCheckbox(string label, ref bool value) => minasisFrame
        ? ui.Checkbox(label, VisibleLabel(label), ref value) : ImGui.Checkbox(label, ref value);
    private bool UiButton(string label) => minasisFrame
        ? ui.Button(label, VisibleLabel(label)) : ImGui.Button(label);
    // The active theme supplies Minasis colors; keep the host's inline step buttons and label layout.
    private bool UiInputInt(string label, ref int value, int step = 1, int stepFast = 100)
        => minasisFrame ? ui.InputInt(label, VisibleLabel(label), ref value, step) : ImGui.InputInt(label, ref value, step, stepFast);
    private bool UiInputText(string label, ref string value, int maxLength) => minasisFrame
        ? ui.InputText(label, VisibleLabel(label), ref value, maxLength) : ImGui.InputText(label, ref value, maxLength);
    private bool UiSlider(string label, ref float value, float min, float max, string format = "%.0f") => minasisFrame
        ? ui.Slider(label, VisibleLabel(label), ref value, min, max, format) : ImGui.SliderFloat(label, ref value, min, max, format);
    private static string VisibleLabel(string label)
    {
        var marker = label.IndexOf("##", StringComparison.Ordinal);
        return marker < 0 ? label : label[..marker];
    }
}
