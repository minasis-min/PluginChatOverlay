using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace PluginChatOverlay;

internal sealed record GameLogFilterEntry(uint Id, string Name, int Kind, ushort Caster, ushort Target, int Category, int Order)
{

    public bool Matches(int kind, int source, int target) => Kind == GameLogCatalog.CanonicalKind(kind)
        && RelationMatches(Caster, source) && RelationMatches(Target, target);
    private static bool RelationMatches(ushort mask, int relation) => relation is >= 0 and < 16 && (mask & (1 << relation)) != 0;
}

/// <summary>The game's own LogFilter rows supply labels, order and relation masks.</summary>
internal sealed class GameLogCatalog
{
    public static readonly string[] Subjects = ["自分", "パーティ", "アライアンス", "他人", "交戦状態の敵", "非交戦状態の敵", "友好的なNPC",
        "自分のペット/バディ", "パーティのペット/バディ", "アライアンスのペット/バディ", "他人のペット/バディ"];
    public GameLogFilterEntry[] Entries { get; }
    private readonly Dictionary<int, GameLogFilterEntry[]> byKind;
    private readonly Dictionary<int, GameLogFilterEntry[]> byCategory;
    public bool Available => Entries.Length > 0;
    public string Diagnostic { get; }

    internal GameLogCatalog(IEnumerable<GameLogFilterEntry> entries, string diagnostic = "ゲームのログフィルターを読み込み済み")
    {
        Entries = entries.Where(e => e.Kind > 0 && e.Name.Length > 0 && e.Order > 0
            && (e.Category is 1 or 2 || e.Category is >= 4 and <= 14)).OrderBy(e => e.Category).ThenBy(e => e.Order).ToArray();
        byKind = Entries.GroupBy(e => e.Kind).ToDictionary(g => g.Key, g => g.ToArray());
        byCategory = Entries.GroupBy(e => e.Category).ToDictionary(g => g.Key, g => g.ToArray());
        Diagnostic = diagnostic;
    }
    public static GameLogCatalog Load(IDataManager data, IPluginLog log)
    {
        try
        {
            var entries = data.GetExcelSheet<LogFilter>().Select(row => new GameLogFilterEntry(row.RowId,
                row.Name.ExtractText(), row.LogKind, row.Caster, row.Target, row.Category, row.DisplayOrder)).ToArray();
            var catalog = new GameLogCatalog(entries);
            if (!catalog.Available) throw new InvalidDataException("LogFilterに対応項目がありません。");
            return catalog;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Native log filters unavailable; original game output is preserved");
            return new([], "ゲームログのフィルターを読み込めませんでした。");
        }
    }
    // These two kinds share one checkbox in the native filter UI.
    public static int CanonicalKind(int kind) => kind switch { 13 => 12, 32 => 14, _ => kind };
    public GameLogFilterEntry[] Category(int category) => byCategory.GetValueOrDefault(category) ?? [];
    public GameLogSelection BuildSelection(IEnumerable<GameLogFilter> filters)
    {
        var selected = filters.Where(f => f.Enabled).SelectMany(f => f.Rows).ToHashSet();
        return new GameLogSelection(Entries.Where(e => selected.Contains(e.Id)));
    }
    public bool Matches(GameLogFilter filter, int kind, int source, int target)
    {
        if (!filter.Enabled || filter.Rows.Count == 0 || !byKind.TryGetValue(CanonicalKind(kind), out var candidates)) return false;
        foreach (var entry in candidates)
            if (filter.Rows.Contains(entry.Id) && entry.Matches(kind, source, target)) return true;
        return false;
    }
}

internal sealed class GameLogFilter
{
    public bool Enabled { get; set; }
    public HashSet<uint> Rows { get; set; } = [];
    public void Normalize() => Rows = (Rows ?? []).Where(id => id is > 0 and <= ushort.MaxValue).Take(512).ToHashSet();
}

/// <summary>Immutable receive-side plan, rebuilt only when settings change, never enumerating mutable UI collections.</summary>
internal sealed class GameLogSelection(IEnumerable<GameLogFilterEntry> entries)
{
    public static readonly GameLogSelection Empty = new([]);
    private readonly Dictionary<int, GameLogFilterEntry[]> byKind = entries.GroupBy(e => e.Kind).ToDictionary(g => g.Key, g => g.ToArray());
    public bool HasSelection => byKind.Count > 0;
    public bool Matches(int kind, int source, int target)
    {
        if (!byKind.TryGetValue(GameLogCatalog.CanonicalKind(kind), out var candidates)) return false;
        foreach (var entry in candidates) if (entry.Matches(kind, source, target)) return true;
        return false;
    }
}
