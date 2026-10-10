using System.Numerics;
using System.Text.Json;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Lumina.Text.ReadOnly;
using PluginChatOverlay;

internal static class PresentationChecks
{
    public static void Run()
    {
        var tags = new PluginTags();
        tags.Reset([("WeaponGlamour", "Weapon Glamour"), ("OtherPlugin", "Other Plugin")]);
        Check(tags.Resolve("WeaponGlamour: 一括適用が完了しました") == "WeaponGlamour", "exact colon name tag recognition");
        Check(tags.Resolve("[Other Plugin] 日本語") == "OtherPlugin", "exact display-name bracket recognition");
        Check(tags.Resolve("他のログ: WeaponGlamour: 一括") == "" && tags.Resolve("WeaponGlamourPlus: 他") == "", "body substring and similar name remain unclassified");
        tags.Reset([("A", "Duplicate"), ("B", "Duplicate")]);
        Check(tags.Resolve("[Duplicate] text") == "", "ambiguous tag remains unclassified");
        var tab = new ChatTab { IncludeUnknown = true, Plugins = ["WeaponGlamour"] };
        Check(tab.Matches("") && tab.Matches("WeaponGlamour") && !tab.Matches("OtherPlugin"), "tab partitions selected plugins and unknown");
        tab.IncludeAll = true;
        Check(tab.Matches("OtherPlugin") && tab.Matches(""), "all tab includes unknown and named logs");
        var screen = new WindowRect(0, 0, 1920, 1080);
        Check(WindowSnap.Position(new(6, 300, 400, 300), screen, [], 16) == new Vector2(0, 300), "snap to screen left edge");
        Check(WindowSnap.Position(new(1509, 786, 400, 280), screen, [], 16) == new Vector2(1520, 800), "snap to screen right and bottom");
        Check(WindowSnap.Position(new(507, 300, 400, 300), screen, [new(100, 300, 400, 300)], 16).X == 500, "snap adjacent window edge");
        Check(WindowSnap.Position(new(507, 700, 400, 250), screen, [new(100, 100, 400, 200)], 16) == new Vector2(507, 700), "distant unrelated window does not snap");
        var selection = new TextSelection { Start = new(1, 2), End = new(2, 2), Dragged = true };
        (long Id, string Text)[] rows = [(1, "日本語です"), (2, "次のログ")];
        Check(selection.Extract(rows) == "語です" + Environment.NewLine + "次の", "drag copy across Japanese log boundaries");
        selection.Active = true; selection.EndDrag();
        Check(!selection.Active && selection.Dragged && selection.Extract(rows) == "語です" + Environment.NewLine + "次の",
            "hiding UI ends active drag while retaining selected text for copy");
        (selection.Start, selection.End) = (selection.End, selection.Start);
        Check(selection.Extract(rows) == "語です" + Environment.NewLine + "次の", "reverse drag same selection");
        selection.Start = new(1, 5); selection.End = new(2, 0);
        Check(selection.Extract(rows) == Environment.NewLine, "selection of inter-record newline retained");
        Check(selection.Extract([(2, "次のログ")]) == "", "selection of evicted record is not redirected");
        Check(TextSelection.Boundary("A😀e\u0301Z", 2) == 1 && TextSelection.Boundary("A😀e\u0301Z", 4) == 3, "selection never splits surrogate or combining cluster");
        var unicodeMap = TextByteMap.Build(System.Text.Encoding.UTF8.GetBytes("日😀e\u0301Z"));
        Check(unicodeMap[0] == new TextCharacter(0, 1) && unicodeMap[3] == new TextCharacter(1, 2) && unicodeMap[7] == new TextCharacter(3, 2) && unicodeMap[8] == new TextCharacter(3, 2) && unicodeMap[10] == new TextCharacter(5, 1), "UTF8 glyph offsets map to Japanese, emoji and combined text clusters");
        GlyphBox[] glyphs = [new(0, 1, Vector2.Zero, new(10, 20)), new(1, 2, new(10, 0), new(20, 20)), new(3, 1, new(0, 20), new(10, 20))];
        Check(FragmentLayout.Caret(glyphs, new(25, 8), 4) == 3 && FragmentLayout.Caret(glyphs, new(2, 25), 4) == 3, "caret uses wrapped glyph rectangles and cluster lengths");
        var record = new ChatRecord(1, 1, 0, 0, 0, [], [], "") { ReceivedAt = new DateTimeOffset(2026, 10, 8, 23, 34, 0, TimeSpan.FromHours(9)) };
        Check(ChatClock.Format(record) == "[23:34]", "local timestamp minute format");
        var format = ReadOnlySeString.FromMacroString("<color(4291611852)><string(lstr2)><color(stackcolor)>");
        var prefix = ChatPalette.FirstColor(format.AsSpan().Data);
        Check(new ReadOnlySeString(prefix).ToMacroString() == "<color(4291611852)>", "default Debug color copied exactly from vanilla format");
        var globalFormat = ReadOnlySeString.FromMacroString("<color(gnum43)><string(lstr2)><color(stackcolor)>");
        Check(new ReadOnlySeString(ChatPalette.FirstColor(globalFormat.AsSpan().Data)).ToMacroString() == "<color(gnum43)>", "default configurable color preserves game global reference");
        var displayRecord = record with { Body = new SeStringBuilder().AddUiForeground(45).AddText("[Orchestrion]").AddUiForegroundOff().AddText(" 本文").Build().Encode() };
        var displaySettings = JsonSerializer.Deserialize<Settings>("{\"PluginStyles\":{\"orchestrion\":{\"Custom\":true,\"OverridePayloadColors\":true}}}")!;
        var legacySettings = JsonSerializer.Deserialize<Settings>("{\"ShowTabs\":false,\"FontSize\":21,\"AutoRecognizeTags\":false,\"TagRules\":[{\"Tag\":\"old\",\"Plugin\":\"old\"}],\"PluginStyles\":{}}")!;
        var compactSettings = JsonSerializer.Serialize(legacySettings);
        Check(!legacySettings.ShowTabs && legacySettings.FontSize == 21 && !compactSettings.Contains("TagRules") && !compactSettings.Contains("PluginStyles") && !compactSettings.Contains("AutoRecognizeTags") && !compactSettings.Contains("\"Position\":") && !compactSettings.Contains("\"Size\":"), "obsolete settings load safely and are omitted on save while active preferences remain");
        var displayBytes = DisplayDocument.Content(displayRecord, displaySettings, prefix);
        var displayMacro = new ReadOnlySeString(displayBytes).ToMacroString();
        Check(displayMacro.StartsWith("<color(4294967295)>[23:34]<color(stackcolor)> <color(4291611852)>") && displayBytes.AsSpan(displayBytes.Length - displayRecord.Body.Length).SequenceEqual(displayRecord.Body), "white timestamp has separate color scope and original body payload remains exact despite legacy overrides");
        displaySettings.TimestampColor = 0xFF112233;
        Check(new ReadOnlySeString(DisplayDocument.Content(displayRecord, displaySettings, prefix)).ToMacroString().StartsWith("<color(4281541137)>[23:34]<color(stackcolor)>"), "custom timestamp color converts RGBA to game BGRA without recoloring body");
        var copyInput = new CopyInput();
        Check(new Settings().CopyWithCtrlC && JsonSerializer.Deserialize<Settings>("{\"FontSize\":21}")!.CopyWithCtrlC,
            "new and legacy settings preserve Ctrl+C copy enabled by default");
        var disabledCopy = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(new Settings { CopyWithCtrlC = false }))!;
        disabledCopy.Normalize();
        Check(!disabledCopy.CopyWithCtrlC, "disabled Ctrl+C copy persists through settings round trip and normalization");
        var toggledCopy = new CopyInput();
        toggledCopy.Suppress(true, true, true, new object()); toggledCopy.Reset();
        Check(!toggledCopy.TakeRequest() && !toggledCopy.Suppress(false, true, true),
            "disabling Ctrl+C drops pending copy and releases consumed chord without suppressing foreign focus");
        var copyContext = new object(); var contextCopy = new CopyInput();
        contextCopy.Suppress(true, true, true, copyContext);
        Check(contextCopy.TakeRequest(out var retainedContext) && ReferenceEquals(retainedContext, copyContext),
            "copy request retains originating view context for focus revalidation");
        contextCopy.Suppress(false, false, false);
        contextCopy.Suppress(true, true, true, copyContext); contextCopy.CancelRequest();
        Check(!contextCopy.TakeRequest() && contextCopy.Suppress(false, true, true),
            "UI hide cancels pending copy while consumed chord remains held until release");
        Check(!copyInput.Suppress(false, true, true) && !copyInput.Suppress(true, false, true), "copy input does not suppress unfocused chord or unmodified C");
        Check(copyInput.Suppress(true, true, true) && copyInput.Suppress(false, true, true) && !copyInput.Suppress(false, true, false) && !copyInput.Suppress(false, true, true), "copy chord stays consumed through focus loss, then release restores game input");
        Check(copyInput.TakeRequest() && !copyInput.TakeRequest(), "framework copy request is handled once after drawing even without ImGui key detection");
        var incoming = new SeStringBuilder().AddText("Sample Characterは").Add(new ItemPayload(100u)).AddText("オーソドックス・レンジャーリストグローブ").Add(RawPayload.LinkTerminator).AddText("を入手した。").Build().Encode();
        var lootExtra = new SeStringBuilder().AddText(" 2/9 of the set ").Add(new ItemPayload(101u)).AddText("オーソドックス・レンジャー・アタイア").Add(RawPayload.LinkTerminator).AddText("!").Build().Encode();
        Check(LootAppend.Split(62, incoming, [..incoming, ..lootExtra], out var extracted) && extracted.SequenceEqual(lootExtra), "mixed acquisition log splits exact appended set notice including item link while original stays intact");
        Check(!LootAppend.Split(10, incoming, [..incoming, ..lootExtra], out _) && !LootAppend.Split(62, incoming, incoming, out _) && !LootAppend.Split(62, incoming, [1, ..incoming, ..lootExtra], out _) && !LootAppend.Split(62, [], lootExtra, out _), "ordinary chat, unchanged loot and non-prefix rewrites are never split");
        var defaults = new Settings();
        var previousHistorySettings = JsonSerializer.Deserialize<Settings>("{\"HistoryLimit\":1000}")!;
        previousHistorySettings.Normalize();
        Check(defaults.HistoryLimit == 100 && new History().Limit == 100,
            "new settings and history default to 100 records");
        Check(JsonSerializer.Deserialize<Settings>("{}")!.HistoryLimit == 100 && previousHistorySettings.HistoryLimit == 1000,
            "missing history limit uses 100 while explicit previous limit is preserved");
        Check(ChatRowGeometry.Advance(17, 17, 4) == 21, "zero spacing preserves distance between chat records");
        Check(ChatRowGeometry.Advance(12, 12, 4) == 16, "negative spacing reduces single-line chat record distance");
        Check(ChatRowGeometry.Advance(24, 12, 4) == 28, "negative spacing preserves measured wrapped chat height");
        Check(ChatRowGeometry.Advance(0, 9, 4) == 13, "empty chat retains positive adjusted line height");
        var spacingSettings = JsonSerializer.Deserialize<Settings>("{\"FontSize\":17,\"ShowTabs\":false}")!;
        spacingSettings.Normalize();
        Check(spacingSettings.LineSpacing == 0 && spacingSettings.LineHeightRatio == 1 && !spacingSettings.ShowTabs,
            "old settings retain existing line spacing and preferences");
        spacingSettings.LineSpacing = 8;
        var spacingRestored = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(spacingSettings))!;
        spacingRestored.Normalize();
        Check(spacingRestored.LineSpacing == 8 && Math.Abs(spacingRestored.FontSize * spacingRestored.LineHeightRatio - 25) < .001f,
            "line spacing persists and adds pixel gap to wrapped line height");
        spacingRestored.LineSpacing = -5; spacingRestored.Normalize();
        var negativeRestored = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(spacingRestored))!;
        negativeRestored.Normalize();
        Check(negativeRestored.LineSpacing == -5 && Math.Abs(negativeRestored.FontSize * negativeRestored.LineHeightRatio - 12) < .001f,
            "negative line spacing persists and reduces wrapped line height");
        spacingRestored.LineSpacing = int.MinValue; spacingRestored.Normalize();
        var minimumSpacing = spacingRestored.LineSpacing;
        spacingRestored.LineSpacing = 100; spacingRestored.Normalize();
        Check(minimumSpacing == -8 && spacingRestored.LineSpacing == 24, "invalid line spacing clamps to safe pixel range");
        spacingRestored.FontSize = 36; spacingRestored.LineSpacing = -18; spacingRestored.Normalize();
        spacingRestored.FontSize = 10; spacingRestored.NormalizeLineSpacing();
        Check(spacingRestored.LineSpacing == -5 && spacingRestored.LineHeightRatio == .5f,
            "font size reduction adjusts negative spacing to keep positive line height");
        var oldLimit = new Settings { HistoryLimit = 10000 };
        oldLimit.Normalize();
        Check(oldLimit.HistoryLimit == 1000, "legacy 10000 history limit migrates to 1000 without changing other settings");
        oldLimit.Tabs[0].X = -10000; oldLimit.Tabs[0].Y = -10000;
        oldLimit.ResetGeometry();
        Check(oldLimit.Tabs[0].X == 160 && oldLimit.Tabs[0].Y == 160 && oldLimit.X == 80,
            "shared geometry reset restores detached and main windows to safe positions");
        Check(defaults.Tabs[0].Detached && !defaults.WindowTabs("main").Any() && defaults.WindowTabs(defaults.Tabs[0].Id).Single() == defaults.Tabs[0], "first tab owns initial window without empty parent");
        var rootTab = new ChatTab { Id = "root", Detached = true, X = 240, Y = 300, Width = 777, Height = 444 };
        var childTab = new ChatTab { Id = "child", ParentWindowId = "root" };
        var siblingTab = new ChatTab { Id = "sibling", ParentWindowId = "root" };
        var grouped = new Settings { Tabs = [rootTab, childTab, siblingTab] };
        grouped.Normalize();
        Check(grouped.WindowTabs("root").Count() == 3 && grouped.WindowOf(childTab) == "root" && !grouped.WindowTabs("main").Any(), "child tabs share root window without spawning parent container");
        var savedGroups = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(grouped))!; savedGroups.Normalize();
        Check(savedGroups.WindowTabs("root").Count() == 3, "window grouping persists across settings reload");
        grouped.RemoveTab(rootTab);
        Check(childTab.Detached && childTab.Width == 777 && childTab.Height == 444 && childTab.X == 240
            && siblingTab.ParentWindowId == "child" && grouped.WindowTabs("child").Count() == 2, "deleting root promotes child and preserves window geometry and sibling tabs");
        var otherRoot = new ChatTab { Id = "other", Detached = true }; grouped.Tabs.Add(otherRoot);
        grouped.Attach(childTab, "other");
        Check(!childTab.Detached && grouped.WindowTabs("other").Count() == 3 && !grouped.WindowTabs("child").Any(), "merging root moves its children to destination without empty source window");
        grouped.Attach(siblingTab, "main");
        Check(grouped.WindowTabs("main").Single() == siblingTab && grouped.WindowTabs("other").Count() == 2, "individual child can move between root and legacy main window");
        siblingTab.ParentWindowId = "missing"; grouped.Normalize();
        Check(grouped.WindowOf(siblingTab) == "main", "invalid saved parent falls back to visible main group");
        var legacyWindows = JsonSerializer.Deserialize<Settings>("""
            {"Tabs":[{"Id":"all","Name":"タブ1"},{"Id":"second","Name":"タブ2","Detached":true,"Width":810,"Height":420}]}
            """)!;
        legacyWindows.Normalize();
        Check(legacyWindows.WindowTabs("main").Single().Id == "all" && legacyWindows.WindowTabs("second").Single().Width == 810,
            "existing main and detached layout loads without forced regrouping or size loss");
        Check(defaults.AllowMove && defaults.AllowResize && defaults.ShowWindowTitle && defaults.HideDuringCutscene && defaults.HideLoggedOut && defaults.LinkTooltips, "requested window, visibility and tooltip defaults are enabled");
        var dragStart = new Vector2(80, 120); var mouseStart = new Vector2(95, 135);
        Check(WindowDrag.Position(dragStart, mouseStart, new(295.2f, 335.4f)) == new Vector2(280, 320)
            && WindowDrag.Position(dragStart, mouseStart, mouseStart) == dragStart, "window drag uses fixed anchors and integer pixels without accumulated frame drift");
        var fontSettings = new Settings();
        var gameFont = new Dalamud.Interface.FontIdentifier.GameFontAndFamilyId(Dalamud.Interface.GameFonts.GameFontFamily.Axis);
        FontChoice.Store(fontSettings, gameFont);
        Check(FontChoice.Restore(fontSettings.FontIdType, fontSettings.FontIdJson) is Dalamud.Interface.FontIdentifier.GameFontAndFamilyId restored && restored.GameFontFamily == gameFont.GameFontFamily, "game font identifier persists without arbitrary type deserialization");
        Check(FontChoice.Restore("", "") == null, "empty font preference uses Dalamud default");
        var systemFont = FontChoice.Restore("SystemFontId", """
            {"EnglishName":"Regular","LocaleNames":{"en-us":"Regular"},"Family":{"EnglishName":"Yu Gothic","LocaleNames":{"en-us":"Yu Gothic"}},"Weight":400,"Stretch":5,"Style":0}
            """)!;
        FontChoice.Store(fontSettings, systemFont);
        var restoredSystem = FontChoice.Restore(fontSettings.FontIdType, fontSettings.FontIdJson)!;
        Check(restoredSystem.Family.EnglishName == "Yu Gothic" && restoredSystem.Weight == 400 && restoredSystem.Stretch == 5 && restoredSystem.Style == 0, "system font family and face survive safe JSON round trip without loading native font data");
        var badFontRejected = false;
        try { FontChoice.Restore("System.IO.FileInfo", "{}"); } catch (FormatException) { badFontRejected = true; }
        Check(badFontRejected, "font restore rejects unrecognized type identifiers");
        var visibilitySettings = new Settings { AllowMove = false, AllowResize = false, HideDuringCutscene = false, HideLoggedOut = false, LinkTooltips = false };
        var visibilitySaved = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(visibilitySettings))!;
        Check(!visibilitySaved.AllowMove && !visibilitySaved.AllowResize && !visibilitySaved.HideDuringCutscene && !visibilitySaved.HideLoggedOut && !visibilitySaved.LinkTooltips, "disabled movement, resizing, visibility and hover options persist");
        var nativeButtons = new Settings { ShowNativeButtons = false, NativeButtonScale = 9, NativeButtonFontSize = 99 };
        nativeButtons.Normalize();
        var nativeSaved = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(nativeButtons))!;
        nativeSaved.Normalize();
        Check(!nativeSaved.ShowNativeButtons && nativeSaved.NativeButtonScale == 2 && nativeSaved.NativeButtonFontSize == 32
            && new Settings().ShowNativeButtons && new Settings().NativeButtonScale == 1 && new Settings().NativeButtonFontSize == 18, "native chat button visibility and size persist");
        Check(defaults.Tabs.Single().Name == "タブ1" && !defaults.CopyOnDragRelease, "first tab and optional drag auto-copy defaults");
        defaults.Tabs[0].Name = "すべて"; defaults.Normalize();
        Check(defaults.Tabs[0].Name == "タブ1", "old untouched first tab migrates to tab 1");
        Check(WindowTitle.MainTitle(true).Split("###")[1] == WindowTitle.MainTitle(false).Split("###")[1] && WindowTitle.MainTitle(false).StartsWith("###"), "title text can hide without changing standard window identity");
        var windowPreferences = new Settings { ShowWindowTitle = false, Pinned = true, ClickThrough = true, Tabs = [new() { Id = "detached", Detached = true, Pinned = true, ClickThrough = true }] };
        var savedWindows = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(windowPreferences))!;
        Check(!savedWindows.ShowWindowTitle && savedWindows.Pinned && savedWindows.ClickThrough && savedWindows.Tabs[0].Pinned && savedWindows.Tabs[0].ClickThrough, "hidden title and standard pin/clickthrough preferences persist for main and detached windows");
        var coloredLink = new SeStringBuilder().Add(new MapLinkPayload(132u, 1u, 1000, 2000)).AddUiForeground(45).AddText("リンク").AddUiForegroundOff().Add(RawPayload.LinkTerminator).Build().Encode();
        var linkedMap = TextByteMap.Build(coloredLink);
        Check(linkedMap.Values.Distinct().OrderBy(c => c.Start).SequenceEqual(new TextCharacter[] { new(0, 1), new(1, 1), new(2, 1) }), "link and foreground payloads do not shift text selection indices");
        var settings = new Settings { Tabs = [new() { Id = "x", Detached = true, Plugins = ["WeaponGlamour"], ClickThrough = true }, new() { Id = "x" }] };
        settings.Normalize();
        var roundTrip = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings))!; roundTrip.Normalize();
        Check(roundTrip.Tabs.Select(t => t.Id).Distinct().Count() == 2 && roundTrip.Tabs[0].Detached && roundTrip.Tabs[0].ClickThrough && roundTrip.Tabs[0].Plugins.Single() == "WeaponGlamour", "tabs, click-through and routing persist with unique ids");
    }
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
}
