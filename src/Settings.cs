using System.Numerics;
using System.Text.Json.Serialization;

namespace PluginChatOverlay;

internal enum OverlayUiStyle { Dalamud, Minasis }

internal sealed class Settings
{
    public OverlayUiStyle UiStyle { get; set; } = OverlayUiStyle.Dalamud;
    public bool Enabled { get; set; } // Initial opt-in for this unverified development build.
    public bool Visible { get; set; } = true;
    public float BackgroundAlpha { get; set; } = .8f;
    public float FontSize { get; set; } = 17;
    public int LineSpacing { get; set; }
    [JsonIgnore] public int MinimumLineSpacing => -(int)MathF.Floor(FontSize / 2);
    [JsonIgnore] public float LineHeightRatio => 1f + LineSpacing / FontSize;
    public bool ShowTime { get; set; } = true;
    public uint TimestampColor { get; set; } = 0xFFFFFFFF;
    public int HistoryLimit { get; set; } = 1000;
    public bool CopyOnDragRelease { get; set; }
    public bool ShowWindowTitle { get; set; } = true;
    public bool ShowTabs { get; set; } = true;
    public bool AllowMove { get; set; } = true;
    public bool AllowResize { get; set; } = true;
    public bool HideDuringCutscene { get; set; } = true;
    public bool HideLoggedOut { get; set; } = true;
    public bool LinkTooltips { get; set; } = true;
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
    public void Normalize()
    {
        if (!Enum.IsDefined(UiStyle)) UiStyle = OverlayUiStyle.Dalamud;
        BackgroundAlpha = float.IsFinite(BackgroundAlpha) ? Math.Clamp(BackgroundAlpha, 0, 1) : .8f;
        FontSize = float.IsFinite(FontSize) ? Math.Clamp(MathF.Round(FontSize), 10, 36) : 17;
        NormalizeLineSpacing();
        HistoryLimit = Math.Clamp(HistoryLimit, 10, History.MaxRecords);
        TimestampColor |= 0xFF000000;
        SnapDistance = float.IsFinite(SnapDistance) ? Math.Clamp(SnapDistance, 4, 40) : 16;
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
