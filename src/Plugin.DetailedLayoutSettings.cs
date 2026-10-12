using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace PluginChatOverlay;

public sealed partial class Plugin
{
    private static readonly string[] LayoutPages = ["背景・外枠", "スクロールバー", "文字・フォント", "リンク・時刻", "チャット横ボタン"];
    private static readonly string LayoutResetIcon = FontAwesomeIcon.Undo.ToIconString();

    private bool DrawLayoutSettings()
    {
        var changed = DrawAppearanceModeSettings();
        if (!ImGui.BeginTabBar("DetailedLayoutTabs")) return changed;
        try
        {
            for (var i = 0; i < LayoutPages.Length; i++)
            {
                if (!ImGui.BeginTabItem(LayoutPages[i])) continue;
                try { changed |= DrawLayoutPage(i); }
                finally { ImGui.EndTabItem(); }
            }
            if (BeginLayoutResetTab())
            {
                try { changed |= DrawLayoutResetSettings(); }
                finally { ImGui.EndTabItem(); }
            }
        }
        finally { ImGui.EndTabBar(); }
        return changed;
    }

    private static readonly MinasisUI.UiChoice[] AppearanceModes = [new("standard", "標準（従来のスタイル）"), new("custom", "カスタム")];
    private static readonly MinasisUI.UiChoice[] BackgroundModes = [new("normal", "通常（基準の背景色）"), new("darken", "乗算（黒）")];
    private bool DrawAppearanceModeSettings()
    {
        var mode = (int)settings.AppearanceMode;
        var changed = AppearanceDropdown("chat-appearance", "チャットの外観", ref mode, AppearanceModes);
        if (changed)
        {
            settings.AppearanceMode = (ChatAppearanceMode)mode;
            InitializeCustomAppearance();
            fontChooser?.Dispose(); fontChooser = null;
            RebuildChatFont();
        }
        ImGui.TextWrapped(settings.IsCustomAppearance
            ? "編集値を適用し、UI切替でも外観を保持します。"
            : "選択中のUIの標準スタイルを使用します。");
        ImGui.TextWrapped("切り替えても編集値は保持されます。");
        return changed;
    }
    private void DrawCustomAppearanceNotice()
    {
        if (!settings.IsCustomAppearance)
            ImGui.TextWrapped("カスタム時のみ編集・適用できます。編集値は保持されます。");
    }
    private bool AppearanceDropdown(string id, string label, ref int selected, MinasisUI.UiChoice[] choices)
    {
        ImGui.SetNextItemWidth(280 * ImGuiHelpers.GlobalScale);
        if (minasisFrame) return ui.Dropdown(id, label, ref selected, choices);
        if (!ImGui.BeginCombo(label + "###" + id, choices[selected].Label)) return false;
        var changed = false;
        try
        {
            for (var i = 0; i < choices.Length; i++)
                if (ImGui.Selectable(choices[i].Label, selected == i)) { selected = i; changed = true; }
        }
        finally { ImGui.EndCombo(); }
        return changed;
    }

    private bool DrawLayoutPage(int page)
    {
        var body = ImGui.BeginChild("DetailedLayoutBody-" + page, Vector2.Zero, false);
        try
        {
            if (!body) return false;
            return page switch
            {
                0 => DrawDetailedWindowAppearance(),
                1 => DrawCustomScrollbarSettings(),
                2 => DrawChatTextSettings(),
                3 => DrawChatLinkSettings(),
                _ => DrawNativeButtonSettings(),
            };
        }
        finally { ImGui.EndChild(); }
    }

    private bool DrawCustomScrollbarSettings()
    {
        DrawCustomAppearanceNotice();
        ImGui.BeginDisabled(!settings.IsCustomAppearance);
        try { return DrawScrollbarAppearance(); }
        finally { ImGui.EndDisabled(); }
    }

    private static bool BeginLayoutResetTab()
    {
        ImGui.PushFont(UiBuilder.IconFont);
        try
        {
            var iconSize = ImGui.CalcTextSize(LayoutResetIcon);
            ImGui.SetNextItemWidth(Math.Max(44 * ImGuiHelpers.GlobalScale,
                iconSize.X + ImGui.GetStyle().FramePadding.X * 2 + 12 * ImGuiHelpers.GlobalScale));
            // The hidden label preserves tab interaction while the icon is centered explicitly.
            var selected = ImGui.BeginTabItem("###LayoutReset", ImGuiTabItemFlags.Trailing | ImGuiTabItemFlags.NoTooltip);
            var min = ImGui.GetItemRectMin(); var max = ImGui.GetItemRectMax();
            ImGui.GetWindowDrawList().AddText(min + (max - min - iconSize) / 2,
                ImGui.GetColorU32(ImGuiCol.Text), LayoutResetIcon);
            return selected;
        }
        finally { ImGui.PopFont(); }
    }

    private bool DrawLayoutResetSettings()
    {
        var body = ImGui.BeginChild("DetailedLayoutResetBody", Vector2.Zero, false);
        try
        {
            if (!body) return false;
            var io = ImGui.GetIO();
            var changed = false;
            ImGui.BeginDisabled(!io.KeyCtrl || !io.KeyShift);
            try
            {
                if (UiButton("レイアウトリセット") && io.KeyCtrl && io.KeyShift)
                {
                    ResetLayoutSettings();
                    changed = true;
                }
            }
            finally { ImGui.EndDisabled(); }
            ImGui.TextWrapped("全てのレイアウト設定を既定に戻します。Ctrl＋Shiftを押しながらボタンをクリックしてください。");
            return changed;
        }
        finally { ImGui.EndChild(); }
    }

    private void ResetLayoutSettings()
    {
        settings.ResetLayout();
        ResetWindowLayoutDefaults();
        fontChooser?.Dispose(); fontChooser = null;
        RebuildChatFont();
    }
}
