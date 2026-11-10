using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using MinasisUI;
using MinasisUI.Workspace;
using Dalamud.Interface.Windowing;

namespace PluginChatOverlay;

public sealed partial class Plugin
{
    private readonly Ui ui;
    private readonly WorkspaceUi workspace;
    private UiTheme? workspaceControlTheme;
    private readonly WindowSystem settingsWindows = new("PCO.Workspace");
    private WorkspaceWindow? workspaceWindow;
    private WorkspaceFonts? workspaceFonts;
    private UiHeadingFonts? headingFonts;
    private bool minasisFrame;
    private ChatPopupStyle popupStyle;
    private readonly ChatStyleSnapshot frameUiStyle = new();
    private uint originalChatColor;
    private static readonly UiChoice[] UiStyles = [new("dalamud", "Dalamud標準"), new("minasis", "MinasisUI")];

    // Capture once per frame so changing the selector cannot mix control styles or unbalance stacks.
    private OverlayThemeScope? BeginUiTheme()
    {
        minasisFrame = settings.UiStyle == OverlayUiStyle.Minasis;
        originalChatColor = ImGui.GetColorU32(ImGuiCol.Text);
        ui.Scale = ImGuiHelpers.GlobalScale;
        if (!minasisFrame) { InitializeCustomAppearance(); popupStyle = ChatPopupStyle.Capture(frameUiStyle); return null; }
        ui.Theme = UiTheme.SoftCharcoal;
        var scope = new OverlayThemeScope(ui);
        InitializeCustomAppearance();
        popupStyle = ChatPopupStyle.Capture(frameUiStyle);
        return scope;
    }
    private void InitializeCustomAppearance()
    {
        var captured = settings.CaptureCustomMessageGap(ImGui.GetStyle().ItemSpacing.Y, ImGuiHelpers.GlobalScale);
        if (settings.IsCustomAppearance && settings.CustomChatStyle == null)
        {
            settings.CustomChatStyle = new();
            settings.CustomChatStyle.Capture(ImGuiHelpers.GlobalScale);
            captured = true;
        }
        if (captured)
            geometryDirtyAt = Environment.TickCount64;
    }
    private bool DrawUiStyleSettings(bool sidebar = false)
    {
        if (!sidebar) UiSection("UIの外観");
        var selected = (int)settings.UiStyle;
        var changed = false;
        changed=UiDropdown("ui-style","UI",ref selected,UiStyles,sidebar ? ImGui.GetContentRegionAvail().X : 240 * ImGuiHelpers.GlobalScale);
        if (changed)
        {
            settings.UiStyle = (OverlayUiStyle)selected;
            // Remeasure line heights even when the wrap width happens to stay unchanged.
            InvalidateRowLayouts();
        }
        if (!sidebar)
        {
            UiDescription("設定画面と標準スタイルの外観を切り替えます。カスタムのチャット外観は保持します。既定はDalamud標準です。");
        }
        return changed;
    }
    private bool inSettingsCard, settingsCardHasHeading;
    private Dictionary<string, float>? settingsCardHeights;
    private bool SettingsCard(string id, Func<bool> draw)
    {
        var changed = false;
        var previous = inSettingsCard; var previousHeading = settingsCardHasHeading;
        inSettingsCard = true; settingsCardHasHeading = false;
        try
        {
            if (minasisFrame) workspace.Card("settings-" + id, "", 180, () => changed |= draw());
            else
            {
                settingsCardHeights ??= new();
                var height = settingsCardHeights.GetValueOrDefault(id, 180 * ImGuiHelpers.GlobalScale);
                var visible = ImGui.BeginChild("settings-card-" + id, new System.Numerics.Vector2(0, height), true);
                try
                {
                    if (visible)
                    {
                        changed |= draw();
                        settingsCardHeights[id] = Math.Max(48 * ImGuiHelpers.GlobalScale, ImGui.GetCursorPosY() + ImGui.GetStyle().WindowPadding.Y);
                    }
                }
                finally { ImGui.EndChild(); }
                ImGui.Spacing();
            }
        }
        finally { inSettingsCard = previous; settingsCardHasHeading = previousHeading; }
        return changed;
    }
    private void UiSection(string label)
    {
        if (minasisFrame && workspace != null) { if (!inSettingsCard || settingsCardHasHeading) workspace.GroupGap(); workspace.Flow.Heading(label, false); settingsCardHasHeading = true; }
        else if (minasisFrame) ui.Heading(label);
        else { ImGui.Spacing(); ImGui.Separator(); ImGui.TextUnformatted(label); ImGui.Spacing(); }
    }
    private bool UiDropdown(string id,string label,ref int selected,IReadOnlyList<UiChoice> choices,float width)
    {
        if(minasisFrame) { ImGui.SetNextItemWidth(width); return ui.Dropdown(id,label,ref selected,choices); }
        ImGui.TextUnformatted(label);
        ImGui.SetNextItemWidth(Math.Min(width,Math.Max(1,ImGui.GetContentRegionAvail().X)));
        var preview=selected>=0 && selected<choices.Count ? choices[selected].Label : "未選択";
        if(!ImGui.BeginCombo("###"+id,preview)) return false;
        var changed=false;
        try
        {
            for(var i=0;i<choices.Count;i++)
            {
                if(ImGui.Selectable(choices[i].Label+"###"+choices[i].Id,selected==i)) { selected=i; changed=true; }
                if(selected==i) ImGui.SetItemDefaultFocus();
            }
        }
        finally { ImGui.EndCombo(); }
        return changed;
    }
    private void UiDescription(string text)
    {
        if(minasisFrame) { if(workspace!=null) workspace.Flow.Muted(text); else ui.Muted(text); return; }
        ImGui.PushStyleColor(ImGuiCol.Text,ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
        try { ImGui.TextWrapped(text); }
        finally { ImGui.PopStyleColor(); }
    }
    private bool UiCheckbox(string label, ref bool value) => minasisFrame
        ? ui.Checkbox(label, VisibleLabel(label), ref value) : ImGui.Checkbox(label, ref value);
    private bool UiButton(string label) => minasisFrame
        ? workspace?.Flow.Button(label, VisibleLabel(label)) ?? ui.Button(label, VisibleLabel(label)) : ImGui.Button(label);
    // The active theme supplies Minasis colors; keep the host's inline step buttons and label layout.
    private bool UiInputInt(string label, ref int value, int step = 1, int stepFast = 100)
        => minasisFrame ? ui.InputInt(label, VisibleLabel(label), ref value, step) : ImGui.InputInt(label, ref value, step, stepFast);
    private bool UiListCheckbox(string label, ref bool value) => minasisFrame && workspace != null
        ? workspace.Flow.CompactCheckbox(label, VisibleLabel(label), ref value) : UiCheckbox(label, ref value);
    private void UiTooltip(string text,System.Numerics.Vector2? anchor = null)
        => UiTooltip(()=>ImGui.TextUnformatted(text),anchor);
    private void UiTooltip(Action content,System.Numerics.Vector2? anchor = null)
    {
        if(!ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) return;
        // The common tooltip supplies 80% background, 14x10 scaled padding and
        // wrapping. Discard sizing/alpha inherited from the chat/button window.
        ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize,System.Numerics.Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize,0);
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha,1);
        try
        {
            ImGui.SetNextWindowSize(System.Numerics.Vector2.Zero,ImGuiCond.Always);
            ImGui.SetNextWindowSizeConstraints(System.Numerics.Vector2.Zero,new(float.MaxValue,float.MaxValue));
            if(anchor is { } position) ImGui.SetNextWindowPos(position,ImGuiCond.Always,new(.5f,1));
            ui.Tooltip(content);
        }
        finally { ImGui.PopStyleVar(3); }
    }
    private bool UiGuardedButton(string label)
    {
        if (minasisFrame && workspace != null) return workspace.Flow.GuardedAction(label, label);
        var clicked = ImGui.Button(label);
        UiTooltip("Ctrl＋Shiftを押しながらクリック");
        return clicked && ImGui.GetIO().KeyCtrl && ImGui.GetIO().KeyShift;
    }
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
