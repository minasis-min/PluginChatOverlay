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
        if (!ImGui.BeginTabBar("DetailedLayoutTabs")) return false;
        var changed = false;
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

    private bool DrawLayoutPage(int page)
    {
        var body = ImGui.BeginChild("DetailedLayoutBody-" + page, Vector2.Zero, false);
        try
        {
            if (!body) return false;
            return page switch
            {
                0 => DrawDetailedWindowAppearance(),
                1 => DrawScrollbarAppearance(),
                2 => DrawChatTextSettings(),
                3 => DrawChatLinkSettings(),
                _ => DrawNativeButtonSettings(),
            };
        }
        finally { ImGui.EndChild(); }
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
