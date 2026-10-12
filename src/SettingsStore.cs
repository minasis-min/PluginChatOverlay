using System.Text.Json;

namespace PluginChatOverlay;

internal sealed class SettingsStore
{
    private readonly string path;
    private readonly string? legacyPath;
    private bool saveBlocked;
    public SettingsStore(string assemblyDirectory)
    {
        if (!string.Equals(Path.GetPathRoot(assemblyDirectory), "D:\\", StringComparison.OrdinalIgnoreCase))
            throw new IOException("設定はDドライブへ保存します。DLLをDドライブへ配置してください。");
        path = Path.Combine(assemblyDirectory, "data", "settings.json");
    }
    private SettingsStore(string path, string? legacyPath) { this.path = path; this.legacyPath = legacyPath; }
    public static SettingsStore ForDistribution(string configDirectory, string assemblyDirectory)
    {
        if (!Path.IsPathFullyQualified(configDirectory)) throw new IOException("設定フォルダの絶対パスが必要です。");
        var legacy = string.Equals(Path.GetPathRoot(assemblyDirectory), "D:\\", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(assemblyDirectory, "data", "settings.json") : null;
        return new(Path.Combine(configDirectory, "settings.json"), legacy);
    }
    public Settings Load()
    {
        // A failed read must never be followed by overwriting the original with defaults.
        saveBlocked = true;
        var migrate = !File.Exists(path) && legacyPath != null && File.Exists(legacyPath);
        var input = File.Exists(path) ? path : migrate ? legacyPath! : null;
        var settings = input != null ? Parse(File.ReadAllText(input)) : new();
        settings.Normalize();
        saveBlocked = false;
        try { if (migrate) Save(settings); }
        catch { saveBlocked = true; throw; }
        return settings;
    }
    internal static Settings Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("設定はJSONオブジェクトである必要があります。");
        var settings = JsonSerializer.Deserialize<Settings>(json) ?? throw new JsonException("設定ファイルがnullです。");
        var missingAppearance = !document.RootElement.TryGetProperty(nameof(Settings.AppearanceMode), out _);
        var missingBackground = !document.RootElement.TryGetProperty(nameof(Settings.BackgroundMode), out _);
        if (missingAppearance || missingBackground)
        {
            // 1.0.5 always saves BackgroundBlur, including false. Once modes are
            // saved, use them directly and no longer infer the previous appearance.
            var recent = document.RootElement.TryGetProperty(nameof(Settings.BackgroundBlur), out _);
            if (missingAppearance) settings.AppearanceMode = recent ? ChatAppearanceMode.Custom : ChatAppearanceMode.Standard;
            if (missingBackground) settings.BackgroundMode = recent ? ChatBackgroundMode.Darken : ChatBackgroundMode.Normal;
        }
        settings.Normalize();
        return settings;
    }
    public void Save(Settings settings)
    {
        if (saveBlocked) throw new IOException("設定読込に失敗したため保存を停止しています。元の設定を修復して再ロードしてください。");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, true);
    }
}
