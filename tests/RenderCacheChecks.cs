using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Plugin.Services;
using PluginChatOverlay;

internal static class RenderCacheChecks
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    public static void Run()
    {
        var resetSettings = new Settings { ConfigWidth = 987, ConfigHeight = 654, Enabled = true, HistoryLimit = 321, CopyWithCtrlC = false,
            FontSize = 29, FontIdType = "saved", FontIdJson = "saved", BackgroundAlpha = .1f,
            ScrollbarOutline = true, ScrollbarPositionX = 77, ScrollbarTopAdjustment = -12,
            CustomWindowLayout = true, InnerPaddingX = 25, TextOutline = true, NativeButtonOffsetX = 123 };
        var preservedTabs = resetSettings.Tabs;
        resetSettings.ResetLayout();
        var defaults = new Settings();        var layoutProperties = new[] { "AppearanceMode", "BackgroundMode", "BackgroundAlpha", "FontSize", "LineSpacing", "CustomMessageGap", "ShowTime", "PadTimestampHour", "TimestampColor", "BackgroundBlur", "BlurStrength", "TextOutline", "OutlineStrength", "OutlineColor", "LetterSpacing", "CustomWindowLayout", "WindowLayoutInitialized", "InnerPaddingX", "InnerPaddingY", "BorderWidth", "BorderColor", "WindowRounding", "ScrollbarLeft", "CustomScrollbar", "ScrollbarWhite", "ScrollbarColor", "ScrollbarOutline", "ScrollbarOutlineColor", "ScrollbarOutlineWidth", "ScrollbarWidth", "ScrollbarOffsetX", "ScrollbarPositionX", "ScrollbarOffsetY", "ScrollbarTopAdjustment", "ScrollbarBottomAdjustment", "ScrollbarDisplay", "LinkTooltips", "ShowNativeButtons", "NativeButtonScale", "NativeButtonFontSize", "NativeButtonOffsetX", "NativeButtonOffsetY", "FontIdType", "FontIdJson" };
        Check(layoutProperties.All(name => Equals(typeof(Settings).GetProperty(name)!.GetValue(resetSettings),
                typeof(Settings).GetProperty(name)!.GetValue(defaults))),
            "layout reset restores font, frame, scrollbar, links, time and shortcut defaults including legacy placement");
        Check(resetSettings.Enabled && resetSettings.HistoryLimit == 321 && !resetSettings.CopyWithCtrlC
            && resetSettings.ConfigWidth == 987 && resetSettings.ConfigHeight == 654 && ReferenceEquals(resetSettings.Tabs, preservedTabs),
            "layout reset preserves capture, history, copy preferences and existing tabs");
        var plugin = (Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
        var history = new History(); history.ApplyLimit(20);
        var data = DispatchProxy.Create<IDataManager, ServiceProxy>();
        var log = DispatchProxy.Create<IPluginLog, ServiceProxy>();
        var palette = new ChatPalette(data, log);
        var cachedDocs = new Dictionary<long, DisplayDocument>();
        for (var i = 0; i < 20; i++)
        {
            history.TryAdd(id => new(id, 0, 0, 0, 0, [], [(byte)'a'], "a"), out _);
        }
        var snapshot = history.Snapshot();
        var docs = snapshot.Select(r => new DisplayDocument(r, "", new Settings(), palette, 0)).ToArray();
        foreach (var doc in docs) cachedDocs.Add(doc.Record.Id, doc);
        Set(plugin, "history", history); Set(plugin, "documents", cachedDocs); Set(plugin, "renderCacheGate", new object());
        Set(plugin, "prunedRemovalRevision", -1L);
        var views = (IDictionary)Activator.CreateInstance(typeof(Plugin).GetField("views", Fields)!.FieldType)!;
        var viewType = typeof(Plugin).GetNestedType("ViewState", BindingFlags.NonPublic)!;
        var rowType = typeof(Plugin).GetNestedType("RowLayout", BindingFlags.NonPublic)!;
        var state = Activator.CreateInstance(viewType, true)!; views.Add("hidden", state); Set(plugin, "views", views);
        var rows = (IDictionary)viewType.GetField("Rows", Fields)!.GetValue(state)!;
        foreach (var doc in docs) rows.Add(doc.Record.Id, Activator.CreateInstance(rowType, [doc])!);
        Set(state, "Documents", docs); Set(state, "FilterSource", docs); Set(state, "FilteredDocuments", docs);
        Set(state, "UnreadDocuments", docs);
        Set(plugin, "renderedSnapshot", snapshot); Set(plugin, "renderedDocuments", docs); Set(plugin, "frameDocuments", docs);
        var prune = typeof(Plugin).GetMethod("PruneHistoryCaches", Fields)!;
        history.TryAdd(id => new(id, 0, 0, 0, 0, [], [(byte)'b'], "b"), out _);
        prune.Invoke(plugin, null);
        Check(!cachedDocs.ContainsKey(snapshot[0].Id) && !rows.Contains(snapshot[0].Id) && rows.Count == 19
            && ((Array)Get(state, "Documents")!).Length == 0 && Get(state, "FilterSource") == null,
            "automatic eviction releases hidden view documents and rows without drawing that tab");
        history.ApplyLimit(10); prune.Invoke(plugin, null);
        Check(rows.Count == 9 && cachedDocs.Count == 9 && !rows.Contains(snapshot[1].Id),
            "history limit reduction purges evicted hidden-view rows immediately");
        var pendingCopy = new CopyInput(); pendingCopy.Suppress(true, true, true, state);
        Set(plugin, "copyInput", pendingCopy);
        var retainedSettings = new Settings { X = 913, Y = 327, Width = 812, FontSize = 25 };
        Set(plugin, "settings", retainedSettings); Set(state, "ResetGeometry", false);
        Set(state, "Unread", 7); Set(state, "Follow", false);
        ((TextSelection)Get(state, "Selection")!).Start = new(snapshot[^1].Id, 0);
        typeof(Plugin).GetMethod("OnCommand", Fields)!.Invoke(plugin, ["/pcoverlay", "reset"]);
        Check(history.Snapshot().Length == 0 && !pendingCopy.TakeRequest()
            && (int)Get(state, "Unread")! == 0 && (bool)Get(state, "Follow")!
            && ((TextSelection)Get(state, "Selection")!).Start == null && !(bool)Get(state, "ResetGeometry")!
            && retainedSettings.X == 913 && retainedSettings.Y == 327 && retainedSettings.Width == 812 && retainedSettings.FontSize == 25,
            "reset command clears history, selection and queued copy without changing geometry or settings");
        Check(cachedDocs.Count == 0 && rows.Count == 0 && Get(plugin, "renderedSnapshot") == null
            && ((Array)Get(plugin, "renderedDocuments")!).Length == 0 && ((Array)Get(plugin, "frameDocuments")!).Length == 0
            && ((Array)Get(state, "FilteredDocuments")!).Length == 0 && Get(state, "UnreadDocuments") == null,
            "history clear releases shared and hidden-view caches without drawing any windows");
        var preferences = new Settings { AppearanceMode = ChatAppearanceMode.Custom }; Set(plugin, "settings", preferences);
        var retainedRow = Activator.CreateInstance(rowType, [docs[0]])!; rows.Add(docs[0].Record.Id, retainedRow);
        var updateRender = typeof(Plugin).GetMethod("UpdateRenderSettings", Fields)!;
        var beforeAppearance = RenderSettingsState.Capture(preferences);
        preferences.BackgroundAlpha = .3f; preferences.BorderWidth = 4; preferences.InnerPaddingX = 20;
        preferences.ScrollbarPositionX = 50; preferences.ScrollbarColor = 0x40112233;
        preferences.ScrollbarOutline = true; preferences.NativeButtonScale = 1.5f; preferences.CopyWithCtrlC = false;
        updateRender.Invoke(plugin, [beforeAppearance]);
        Check((int)Get(plugin, "presentationRevision")! == 0 && ReferenceEquals(rows[docs[0].Record.Id], retainedRow),
            "background, frame, scrollbar, shortcut and copy edits keep existing document revision and row cache");
        var beforeTime = RenderSettingsState.Capture(preferences); preferences.TimestampColor = 0xFF123456;
        updateRender.Invoke(plugin, [beforeTime]);
        Check((int)Get(plugin, "presentationRevision")! == 1,
            "timestamp color edits still invalidate formatted documents");
        var beforeClockFormat = RenderSettingsState.Capture(preferences); preferences.PadTimestampHour = false;
        updateRender.Invoke(plugin, [beforeClockFormat]);
        Check((int)Get(plugin, "presentationRevision")! == 2,
            "timestamp format edit invalidates existing displayed history");
        var beforeEdge = RenderSettingsState.Capture(preferences); preferences.TextOutline = true;
        updateRender.Invoke(plugin, [beforeEdge]);
        Check((int)Get(plugin, "presentationRevision")! == 2 && rows.Count == 0,
            "text edge edits remeasure rows without reparsing every document");
    }
    private static object? Get(object target, string name) => target.GetType().GetField(name, Fields)!.GetValue(target);
    private static void Set(object target, string name, object? value) => target.GetType().GetField(name, Fields)!.SetValue(target, value);
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
}
