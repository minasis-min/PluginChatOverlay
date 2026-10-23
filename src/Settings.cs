using System.Numerics;
using System.Text.Json.Serialization;

namespace PluginChatOverlay;

internal enum OverlayUiStyle { Dalamud, Minasis }
internal enum ChatAppearanceMode { Standard, Custom }
internal enum ChatBackgroundMode { Normal, Darken }

internal sealed class Settings
{
    internal const float DefaultConfigWidth = 740;
    internal const float DefaultConfigHeight = 540;
    public OverlayUiStyle UiStyle { get; set; } = OverlayUiStyle.Dalamud;
    public ChatAppearanceMode AppearanceMode { get; set; }
    public ChatBackgroundMode BackgroundMode { get; set; }
    [JsonIgnore] public bool IsCustomAppearance => AppearanceMode == ChatAppearanceMode.Custom;
    [JsonIgnore] public bool UseCustomWindowLayout => IsCustomAppearance && CustomWindowLayout;
    [JsonIgnore] public bool UseBackgroundBlur => IsCustomAppearance && BackgroundBlur;
    [JsonIgnore] public bool UseTextOutline => IsCustomAppearance && TextOutline;
    [JsonIgnore] public int EffectiveLetterSpacing => IsCustomAppearance ? LetterSpacing : 0;
    [JsonIgnore] public bool EffectivePadTimestampHour => !IsCustomAppearance || PadTimestampHour;
    [JsonIgnore] public int EffectiveScrollbarDisplay => IsCustomAppearance ? ScrollbarDisplay : 1;
    [JsonIgnore] public bool DarkenBackground => IsCustomAppearance && BackgroundMode == ChatBackgroundMode.Darken;
    public bool Enabled { get; set; } // Initial opt-in for this unverified development build.
    public bool CopyGameLogs { get; set; }
    public bool Visible { get; set; } = true;
    public bool MainWindowVisible { get; set; } = true;
    public float ConfigWidth { get; set; } = DefaultConfigWidth;
    public float ConfigHeight { get; set; } = DefaultConfigHeight;
    public float BackgroundAlpha { get; set; } = .8f;
    public float FontSize { get; set; } = 17;
    public int LineSpacing { get; set; }
    // Logical UI units, captured once so changing the control theme does not
    // change custom message spacing. UI scale still follows the previous behavior.
    public float? CustomMessageGap { get; set; }
    public ChatStyleSnapshot? CustomChatStyle { get; set; }
    public bool CaptureCustomMessageGap(float themeGap, float scale)
    {
        if (!IsCustomAppearance || CustomMessageGap.HasValue) return false;
        CustomMessageGap = Math.Clamp(themeGap / ValidUiScale(scale), 0, 80);
        return true;
    }
    public float ResolveMessageGap(float themeGap, float scale) => IsCustomAppearance && CustomMessageGap is { } gap
        ? gap * ValidUiScale(scale) : themeGap;
    private static float ValidUiScale(float scale) => float.IsFinite(scale) && scale > 0 ? scale : 1;
    [JsonIgnore] public int MinimumLineSpacing => -(int)MathF.Floor(FontSize / 2);
    [JsonIgnore] public float LineHeightRatio => 1f + LineSpacing / FontSize;
    public bool ShowTime { get; set; } = true;
    public bool PadTimestampHour { get; set; } = true;
    public uint TimestampColor { get; set; } = 0xFFFFFFFF;
    public int HistoryLimit { get; set; } = History.DefaultRecords;
    public int GameHistoryLimit { get; set; } = History.DefaultRecords;
    public bool CopyOnDragRelease { get; set; }
    public bool CopyWithCtrlC { get; set; } = true;
    public bool ShowWindowTitle { get; set; } = true;
    public bool ShowTabs { get; set; } = true;
    public bool AllowMove { get; set; } = true;
    public bool AltDragMove { get; set; } = true;
    public bool AllowResize { get; set; } = true;
    public bool HideDuringCutscene { get; set; } = true;
    public bool HideLoggedOut { get; set; } = true;
    public bool HideWhenNativeChatExpanded { get; set; }
    public bool BackgroundBlur { get; set; }
    public float BlurStrength { get; set; } = 1.5f;
    public bool TextOutline { get; set; }
    public float OutlineStrength { get; set; } = 1;
    public uint OutlineColor { get; set; } = 0xFF000000;
    public int LetterSpacing { get; set; }
    public bool CustomWindowLayout { get; set; }
    public bool WindowLayoutInitialized { get; set; }
    public int InnerPaddingX { get; set; } = 8;
    public int InnerPaddingY { get; set; } = 8;
    public int BorderWidth { get; set; } = 1;
    public uint BorderColor { get; set; } = 0x806E6E80;
    public int WindowRounding { get; set; } = 4;
    public bool ScrollbarLeft { get; set; }
    public bool CustomScrollbar { get; set; }
    public bool ScrollbarWhite { get; set; }
    public uint? ScrollbarColor { get; set; }
    public bool ScrollbarOutline { get; set; }
    public uint ScrollbarOutlineColor { get; set; } = 0xFF8AD5E6; // Warm vanilla-inspired yellow (#E6D58A).
    public int ScrollbarOutlineWidth { get; set; } = 1;
    public int ScrollbarWidth { get; set; } = 14;
    public int ScrollbarOffsetX { get; set; }
    public int? ScrollbarPositionX { get; set; }
    public int ScrollbarOffsetY { get; set; }
    public int? ScrollbarTopAdjustment { get; set; }
    public int? ScrollbarBottomAdjustment { get; set; }
    public int ScrollbarDisplay { get; set; } = 1; // 0: hidden, 1: while scrolling, 2: always
    public bool LinkTooltips { get; set; } = true;
    public bool ShowNativeButtons { get; set; } = true;
    public float NativeButtonScale { get; set; } = 1;
    public float NativeButtonFontSize { get; set; } = 18;
    public int NativeButtonOffsetX { get; set; }
    public int NativeButtonOffsetY { get; set; }
    public string FontIdType { get; set; } = "";
    public string FontIdJson { get; set; } = "";
    public bool Pinned { get; set; }
    public bool ClickThrough { get; set; }
    public bool SnapWindows { get; set; } = true;
    public float SnapDistance { get; set; } = 16;
    public List<ChatTab> Tabs { get; set; } = [new() { Id = "all", Name = "タブ1", IncludeAll = true, Detached = true }];
    public float X { get; set; } = 80;
    public float Y { get; set; } = 120;
    public float Width { get; set; } = 650;
    public float Height { get; set; } = 360;
    [JsonIgnore] public Vector2 Position => new(X, Y);
    [JsonIgnore] public Vector2 Size => new(Width, Height);
    // Retain the saved legacy flag, but direct edits no longer require an opt-in checkbox.
    [JsonIgnore] public bool UsesCustomScrollbar => IsCustomAppearance && (CustomScrollbar
        || ScrollbarLeft || ScrollbarOutline || ScrollbarWidth != 14
        || ScrollbarPositionX.HasValue || ScrollbarOffsetX != 0 || ScrollbarOffsetY != 0
        || ScrollbarTopAdjustment.GetValueOrDefault() != 0 || ScrollbarBottomAdjustment.GetValueOrDefault() != 0);
    public uint ResolveScrollbarColor(uint themeColor) => IsCustomAppearance ? ScrollbarColor ?? (ScrollbarWhite ? 0xFFFFFFFFu : themeColor) : themeColor;
    public void ResetLayout()
    {
        var defaults = new Settings();
        AppearanceMode = defaults.AppearanceMode;
        BackgroundMode = defaults.BackgroundMode;
        BackgroundAlpha = defaults.BackgroundAlpha;
        FontSize = defaults.FontSize;
        LineSpacing = defaults.LineSpacing;
        CustomMessageGap = defaults.CustomMessageGap;
        CustomChatStyle = null;
        ShowTime = defaults.ShowTime;
        PadTimestampHour = defaults.PadTimestampHour;
        TimestampColor = defaults.TimestampColor;
        BackgroundBlur = defaults.BackgroundBlur;
        BlurStrength = defaults.BlurStrength;
        TextOutline = defaults.TextOutline;
        OutlineStrength = defaults.OutlineStrength;
        OutlineColor = defaults.OutlineColor;
        LetterSpacing = defaults.LetterSpacing;
        CustomWindowLayout = defaults.CustomWindowLayout;
        WindowLayoutInitialized = defaults.WindowLayoutInitialized;
        InnerPaddingX = defaults.InnerPaddingX;
        InnerPaddingY = defaults.InnerPaddingY;
        BorderWidth = defaults.BorderWidth;
        BorderColor = defaults.BorderColor;
        WindowRounding = defaults.WindowRounding;
        ScrollbarLeft = defaults.ScrollbarLeft;
        CustomScrollbar = defaults.CustomScrollbar;
        ScrollbarWhite = defaults.ScrollbarWhite;
        ScrollbarColor = defaults.ScrollbarColor;
        ScrollbarOutline = defaults.ScrollbarOutline;
        ScrollbarOutlineColor = defaults.ScrollbarOutlineColor;
        ScrollbarOutlineWidth = defaults.ScrollbarOutlineWidth;
        ScrollbarWidth = defaults.ScrollbarWidth;
        ScrollbarOffsetX = defaults.ScrollbarOffsetX;
        ScrollbarPositionX = defaults.ScrollbarPositionX;
        ScrollbarOffsetY = defaults.ScrollbarOffsetY;
        ScrollbarTopAdjustment = defaults.ScrollbarTopAdjustment;
        ScrollbarBottomAdjustment = defaults.ScrollbarBottomAdjustment;
        ScrollbarDisplay = defaults.ScrollbarDisplay;
        LinkTooltips = defaults.LinkTooltips;
        ShowNativeButtons = defaults.ShowNativeButtons;
        NativeButtonScale = defaults.NativeButtonScale;
        NativeButtonFontSize = defaults.NativeButtonFontSize;
        NativeButtonOffsetX = defaults.NativeButtonOffsetX;
        NativeButtonOffsetY = defaults.NativeButtonOffsetY;
        FontIdType = defaults.FontIdType;
        FontIdJson = defaults.FontIdJson;
    }
    public void Normalize()
    {
        if (!Enum.IsDefined(UiStyle)) UiStyle = OverlayUiStyle.Dalamud;
        if (!Enum.IsDefined(AppearanceMode)) AppearanceMode = ChatAppearanceMode.Standard;
        if (!Enum.IsDefined(BackgroundMode)) BackgroundMode = ChatBackgroundMode.Normal;
        ConfigWidth = float.IsFinite(ConfigWidth) ? Math.Clamp(ConfigWidth, 400, 8000) : DefaultConfigWidth;
        ConfigHeight = float.IsFinite(ConfigHeight) ? Math.Clamp(ConfigHeight, 300, 8000) : DefaultConfigHeight;
        BackgroundAlpha = float.IsFinite(BackgroundAlpha) ? Math.Clamp(BackgroundAlpha, 0, 1) : .8f;
        FontSize = float.IsFinite(FontSize) ? Math.Clamp(MathF.Round(FontSize), 10, 36) : 17;
        NormalizeLineSpacing();
        if (CustomChatStyle != null && !CustomChatStyle.IsValid) CustomChatStyle = null;
        if (CustomMessageGap is { } gap) CustomMessageGap = float.IsFinite(gap) ? Math.Clamp(gap, 0, 80) : null;
        LetterSpacing = Math.Clamp(LetterSpacing, -2, 12);
        InnerPaddingX = Math.Clamp(InnerPaddingX, 0, 40);
        InnerPaddingY = Math.Clamp(InnerPaddingY, 0, 40);
        BorderWidth = Math.Clamp(BorderWidth, 0, 8);
        WindowRounding = Math.Clamp(WindowRounding, 0, 24);
        ScrollbarWidth = Math.Clamp(ScrollbarWidth, 2, 24);
        ScrollbarOutlineWidth = Math.Clamp(ScrollbarOutlineWidth, 1, 4);
        ScrollbarOffsetX = Math.Clamp(ScrollbarOffsetX, -200, 200);
        if (ScrollbarPositionX is { } barX) ScrollbarPositionX = Math.Clamp(barX, 0, 20000);
        ScrollbarOffsetY = Math.Clamp(ScrollbarOffsetY, -200, 200);
        if (ScrollbarTopAdjustment is { } top) ScrollbarTopAdjustment = Math.Clamp(top, -200, 200);
        if (ScrollbarBottomAdjustment is { } bottom) ScrollbarBottomAdjustment = Math.Clamp(bottom, -200, 200);
        ScrollbarDisplay = Math.Clamp(ScrollbarDisplay, 0, 2);
        BlurStrength = float.IsFinite(BlurStrength) ? Math.Clamp(BlurStrength, .1f, 24) : 1.5f;
        OutlineStrength = float.IsFinite(OutlineStrength) ? Math.Clamp(OutlineStrength, .1f, 3) : 1;
        HistoryLimit = Math.Clamp(HistoryLimit, 10, History.MaxRecords);
        TimestampColor |= 0xFF000000;
        SnapDistance = float.IsFinite(SnapDistance) ? Math.Clamp(SnapDistance, 4, 40) : 16;
        NativeButtonScale = float.IsFinite(NativeButtonScale) ? Math.Clamp(NativeButtonScale, .5f, 2f) : 1f;
        NativeButtonFontSize = float.IsFinite(NativeButtonFontSize) ? Math.Clamp(MathF.Round(NativeButtonFontSize), 8, 32) : 18;
        NativeButtonOffsetX = Math.Clamp(NativeButtonOffsetX, -2000, 2000);
        NativeButtonOffsetY = Math.Clamp(NativeButtonOffsetY, -2000, 2000);
        GameHistoryLimit = Math.Clamp(GameHistoryLimit, 10, History.MaxRecords);
        Tabs ??= [];
        Tabs = Tabs.Where(t => t != null).Take(8).ToList();
        if (Tabs.Count == 0) Tabs.Add(new() { Id = "all", Name = "タブ1", IncludeAll = true, Detached = true });
        var ids = new HashSet<string>();
        foreach (var tab in Tabs)
        {
            if (tab.Id == "all" && tab.Name == "すべて") tab.Name = "タブ1";
            if (string.IsNullOrWhiteSpace(tab.Id) || !ids.Add(tab.Id)) { tab.Id = Guid.NewGuid().ToString("N"); ids.Add(tab.Id); }
            tab.Name = string.IsNullOrWhiteSpace(tab.Name) ? "チャット" : tab.Name[..Math.Min(48, tab.Name.Length)];
            tab.Plugins ??= []; tab.Plugins = tab.Plugins.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().Take(256).ToList();
            tab.GameLogs ??= new(); tab.GameLogs.Normalize();
            tab.ParentWindowId ??= "";
            tab.X = float.IsFinite(tab.X) ? tab.X : 160; tab.Y = float.IsFinite(tab.Y) ? tab.Y : 160;
            tab.Width = float.IsFinite(tab.Width) ? Math.Clamp(tab.Width, 250, 4000) : 600;
            tab.Height = float.IsFinite(tab.Height) ? Math.Clamp(tab.Height, 180, 3000) : 350;
        }
        foreach (var tab in Tabs)
            if (tab.Detached || !Tabs.Any(t => t.Detached && t.Id == tab.ParentWindowId)) tab.ParentWindowId = "";
        if (!float.IsFinite(X) || !float.IsFinite(Y)) { X = 80; Y = 120; }
        Width = float.IsFinite(Width) ? Math.Clamp(Width, 250, 4000) : 650;
        Height = float.IsFinite(Height) ? Math.Clamp(Height, 180, 3000) : 360;
    }
    public IEnumerable<ChatTab> WindowTabs(string windowId) => Tabs.Where(t =>
        windowId == "main" ? !t.Detached && t.ParentWindowId.Length == 0 :
        t.Detached && t.Id == windowId || !t.Detached && t.ParentWindowId == windowId);
    public string WindowOf(ChatTab tab) => tab.Detached ? tab.Id : tab.ParentWindowId.Length > 0 ? tab.ParentWindowId : "main";
    public bool IsWindowVisible(string id) => Visible && (id == "main"
        ? MainWindowVisible && WindowTabs("main").Any()
        : Tabs.Any(t => t.Detached && t.Id == id && t.Visible));
    public bool ToggleWindowVisibility(string id)
    {
        var tab = id == "main" ? null : Tabs.FirstOrDefault(t => t.Detached && t.Id == id);
        if (id == "main" ? !WindowTabs("main").Any() : tab == null) return false;
        var wasVisible = IsWindowVisible(id);
        if (!Visible)
        {
            // Restore only the chosen window; all others were effectively hidden.
            MainWindowVisible = false;
            foreach (var root in Tabs.Where(t => t.Detached)) root.Visible = false;
            Visible = true;
        }
        if (id == "main") MainWindowVisible = !wasVisible;
        else tab!.Visible = !wasVisible;
        return true;
    }
    public void ResetGeometry()
    {
        X = 80; Y = 120; Width = 650; Height = 360;
        foreach (var tab in Tabs) { tab.X = 160; tab.Y = 160; tab.Width = 600; tab.Height = 350; }
    }
    public void Attach(ChatTab tab, string destination)
    {
        if (destination == tab.Id) return;
        if (destination != "main" && !Tabs.Any(t => t.Detached && t.Id == destination)) destination = "main";
        if (destination != "main") Tabs.First(t => t.Id == destination).Visible = true;
        else MainWindowVisible = true;
        foreach (var child in Tabs.Where(t => !t.Detached && t.ParentWindowId == tab.Id))
            child.ParentWindowId = destination == "main" ? "" : destination;
        tab.Detached = false; tab.ParentWindowId = destination == "main" ? "" : destination;
    }
    public void NormalizeLineSpacing() => LineSpacing = Math.Clamp(LineSpacing, MinimumLineSpacing, 24);
    public void RemoveTab(ChatTab tab)
    {
        var children = Tabs.Where(t => !t.Detached && t.ParentWindowId == tab.Id).ToArray();
        if (children.Length > 0)
        {
            var root = children[0]; root.Detached = true; root.ParentWindowId = "";
            root.X = tab.X; root.Y = tab.Y; root.Width = tab.Width; root.Height = tab.Height;
            root.Visible = tab.Visible; root.Pinned = tab.Pinned; root.ClickThrough = tab.ClickThrough;
            foreach (var child in children.Skip(1)) child.ParentWindowId = root.Id;
        }
        Tabs.Remove(tab);
    }
}

internal sealed class ChatTab
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "チャット";
    public bool IncludeAll { get; set; }
    public bool IncludeUnknown { get; set; }
    public List<string> Plugins { get; set; } = [];
    public GameLogFilter GameLogs { get; set; } = new();
    public bool Detached { get; set; }
    public string ParentWindowId { get; set; } = "";
    public bool Visible { get; set; } = true;
    public bool ClickThrough { get; set; }
    public bool Pinned { get; set; }
    public float X { get; set; } = 160;
    public float Y { get; set; } = 160;
    public float Width { get; set; } = 600;
    public float Height { get; set; } = 350;
    public bool Matches(string plugin) => IncludeAll || (plugin.Length == 0 ? IncludeUnknown : Plugins.Contains(plugin));
}
