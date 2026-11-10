using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace PluginChatOverlay;

public sealed partial class Plugin
{
    private int gameFilterSubject, gameFilterCategory;
    private static readonly MinasisUI.UiChoice[] GameSubjectChoices = GameLogCatalog.Subjects.Select((s, i) => new MinasisUI.UiChoice(i.ToString(), s)).ToArray();
    private static readonly MinasisUI.UiChoice[] GameCategoryChoices = [new("chat", "チャット"), new("battle", "バトル"), new("announce", "アナウンス")];
    private bool DrawGameLogFilters(ChatTab tab)
    {
        UiSection("ゲームログ");
        var filter = tab.GameLogs;
        var enabled = filter.Enabled;
        var changed = false;
        if (UiCheckbox("このタブにゲームログを表示", ref enabled)) { filter.Enabled = enabled; changed = true; }
        var showMapMessages = filter.ShowMapMessages;
        if (UiCheckbox("マップリンク付きの発言を表示", ref showMapMessages)) { filter.ShowMapMessages = showMapMessages; changed = true; }
        UiDescription("マップリンク付きの発言は、ゲームログの表示とは別に選べます。");
        if (!settings.CopyGameLogs) UiDescription("カテゴリで選んだログの取り込みには「基本」の「ゲームログをコピー」をONにしてください。");
        UiDescription("選択したログをコピーします。ゲーム側のログ・フィルターは変わりません。");
        if (!gameLogCatalog.Available) { UiDescription(gameLogCatalog.Diagnostic); return changed; }
        ImGui.BeginDisabled(!filter.Enabled);
        try
        {
            changed |= DrawGameLogBulkActions(filter);

            UiDropdown("game-category","分類",ref gameFilterCategory,GameCategoryChoices,280 * ImGuiHelpers.GlobalScale);
            if (gameFilterCategory == 1)
            {
                UiDropdown("battle-subject","主体",ref gameFilterSubject,GameSubjectChoices,280 * ImGuiHelpers.GlobalScale);
            }
            changed |= DrawGameLogCategory(filter, gameFilterCategory == 0 ? 1 : gameFilterCategory == 2 ? 2 : gameFilterSubject + 4);
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
                if (!UiListCheckbox(entry.Name + "###game-filter-" + entry.Id, ref selected)) continue;
                if (selected) filter.Rows.Add(entry.Id); else filter.Rows.Remove(entry.Id);
                changed = true;
            }
        }
        finally { ImGui.EndChild(); }
        return changed;
    }
}
