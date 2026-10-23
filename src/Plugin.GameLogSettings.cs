using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace PluginChatOverlay;

public sealed partial class Plugin
{
    private int gameFilterSubject;
    private static readonly MinasisUI.UiChoice[] GameSubjectChoices = GameLogCatalog.Subjects.Select((s, i) => new MinasisUI.UiChoice(i.ToString(), s)).ToArray();
    private static readonly string GameSubjectLabels = string.Join('\0', GameLogCatalog.Subjects) + '\0';
    private bool DrawTabLogFilters(ChatTab tab)
    {
        UiSection("表示するログ");
        var changed = false;
        if (!ImGui.BeginTabBar("LogSources")) return false;
        try
        {
            if (ImGui.BeginTabItem("プラグイン通知"))
            {
                try { changed |= DrawPluginLogFilters(tab); }
                finally { ImGui.EndTabItem(); }
            }
            if (ImGui.BeginTabItem("ゲームログ"))
            {
                try { changed |= DrawGameLogFilters(tab); }
                finally { ImGui.EndTabItem(); }
            }
        }
        finally { ImGui.EndTabBar(); }
        return changed;
    }
    private bool DrawGameLogFilters(ChatTab tab)
    {
        var filter = tab.GameLogs;
        var enabled = filter.Enabled;
        var changed = false;
        if (UiCheckbox("このタブにゲームログを表示", ref enabled)) { filter.Enabled = enabled; changed = true; }
        if (!settings.CopyGameLogs) ImGui.TextWrapped("取り込みには「基本」の「ゲームログをコピー」をONにしてください。");
        ImGui.TextWrapped("選択したログをコピーします。ゲーム側のログ・フィルターは変わりません。");
        if (!gameLogCatalog.Available) { ImGui.TextWrapped(gameLogCatalog.Diagnostic); return changed; }
        ImGui.BeginDisabled(!filter.Enabled);
        try
        {
            changed |= DrawGameLogBulkActions(filter);
            if (ImGui.BeginTabBar("GameLogCategories"))
            {
                try
                {
                    if (ImGui.BeginTabItem("チャット"))
                    { try { changed |= DrawGameLogCategory(filter, 1); } finally { ImGui.EndTabItem(); } }
                    if (ImGui.BeginTabItem("バトル"))
                    {
                        try
                        {
                            ImGui.SetNextItemWidth(280 * ImGuiHelpers.GlobalScale);
                            if (minasisFrame)
                            {
                                // The same values and choices are shared by both control styles.
                                ui.Dropdown("battle-subject", "主体", ref gameFilterSubject, GameSubjectChoices);
                            }
                            else ImGui.Combo("主体", ref gameFilterSubject, GameSubjectLabels);
                            changed |= DrawGameLogCategory(filter, gameFilterSubject + 4);
                        }
                        finally { ImGui.EndTabItem(); }
                    }
                    if (ImGui.BeginTabItem("アナウンス"))
                    { try { changed |= DrawGameLogCategory(filter, 2); } finally { ImGui.EndTabItem(); } }
                }
                finally { ImGui.EndTabBar(); }
            }
        }
        finally { ImGui.EndDisabled(); }
        return changed;
    }
    private bool DrawGameLogBulkActions(GameLogFilter filter)
    {
        var changed = false;
        if (UiButton("全項目を選択###all-game-filters-on"))
        { filter.Rows.UnionWith(gameLogCatalog.Entries.Select(e => e.Id)); changed = true; }
        ImGui.SameLine();
        if (UiButton("全項目を解除###all-game-filters-off")) { filter.Rows.Clear(); changed = true; }
        return changed;
    }
    private bool DrawGameLogCategory(GameLogFilter filter, int category)
    {
        var entries = gameLogCatalog.Category(category);
        var changed = false;
        if (UiButton("この一覧を選択")) { filter.Rows.UnionWith(entries.Select(e => e.Id)); changed = true; }
        ImGui.SameLine();
        if (UiButton("この一覧を解除")) { filter.Rows.ExceptWith(entries.Select(e => e.Id)); changed = true; }
        var visible = ImGui.BeginChild("NativeFilterEntries", new(0, 290 * ImGuiHelpers.GlobalScale), false);
        try
        {
            if (visible) foreach (var entry in entries)
            {
                var selected = filter.Rows.Contains(entry.Id);
                if (!UiCheckbox(entry.Name + "###game-filter-" + entry.Id, ref selected)) continue;
                if (selected) filter.Rows.Add(entry.Id); else filter.Rows.Remove(entry.Id);
                changed = true;
            }
        }
        finally { ImGui.EndChild(); }
        return changed;
    }
}
