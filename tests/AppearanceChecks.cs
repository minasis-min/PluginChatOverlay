using System.Text.Json;
using System.Numerics;
using PluginChatOverlay;

internal static class AppearanceChecks
{
    public static void Run()
    {
        var legacy = JsonSerializer.Deserialize<Settings>("{\"FontSize\":23,\"LineSpacing\":-4,\"TimestampColor\":4294967295,\"ShowTabs\":false}")!;
        legacy.Normalize();
        Check(legacy.FontSize == 23 && legacy.LineSpacing == -4 && !legacy.ShowTabs
            && legacy.TimestampColor == uint.MaxValue && legacy.LetterSpacing == 0
            && !legacy.TextOutline && !legacy.BackgroundBlur && legacy.ScrollbarDisplay == 1
            && !legacy.CustomWindowLayout && !legacy.CustomScrollbar,
            "appearance migration preserves old fonts, line spacing, tabs and time with opt-in effects");
        Check(!legacy.ScrollbarOutline && legacy.ScrollbarOutlineColor == 0xFF8AD5E6 && legacy.ScrollbarOutlineWidth == 1,
            "scrollbar outline defaults off with warm yellow and one pixel thickness");
        var directBar = new Settings();
        Check(!directBar.UsesCustomScrollbar, "untouched scrollbar keeps the standard renderer");
        directBar.ScrollbarColor = 0x40112233;
        Check(!directBar.CustomScrollbar && !directBar.UsesCustomScrollbar && directBar.ResolveScrollbarColor(uint.MaxValue) == 0x40112233,
            "direct color edits preserve the standard renderer without the legacy customization switch");
        directBar.ResetLayout(); directBar.ScrollbarPositionX = 0;
        Check(!directBar.CustomScrollbar && directBar.UsesCustomScrollbar,
            "direct position edits including zero take effect without the customization switch");
        directBar.ResetLayout();
        Check(!directBar.UsesCustomScrollbar, "layout reset restores standard scrollbar defaults");
        var original = new Settings { CustomWindowLayout = true, CustomScrollbar = true, LetterSpacing = -2, TextOutline = true, OutlineColor = 0xFF112233,
            OutlineStrength = .5f, BackgroundBlur = true, BlurStrength = 2.5f,
            InnerPaddingX = 0, InnerPaddingY = 12, BorderWidth = 0, WindowRounding = 0,
            WindowLayoutInitialized = true, ScrollbarDisplay = 2, ScrollbarLeft = true, ScrollbarWhite = true, ScrollbarWidth = 3,
            ScrollbarOffsetX = -10, ScrollbarOffsetY = -12, ScrollbarPositionX = 4,
            ScrollbarTopAdjustment = 3, ScrollbarBottomAdjustment = -4, ScrollbarColor = 0x40112233,
            ScrollbarOutline = true, ScrollbarOutlineColor = 0x80334455, ScrollbarOutlineWidth = 2 };
        var restored = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(original))!;
        restored.Normalize();
        Check(restored.CustomWindowLayout && restored.CustomScrollbar && restored.LetterSpacing == -2 && restored.TextOutline && restored.OutlineColor == 0xFF112233
            && restored.OutlineStrength == .5f && restored.BackgroundBlur && restored.BlurStrength == 2.5f
            && restored.InnerPaddingX == 0 && restored.InnerPaddingY == 12 && restored.BorderWidth == 0
            && restored.WindowRounding == 0 && restored.ScrollbarDisplay == 2 && restored.ScrollbarLeft
            && restored.ScrollbarWhite && restored.ScrollbarWidth == 3 && restored.WindowLayoutInitialized
            && restored.ScrollbarOffsetX == -10 && restored.ScrollbarOffsetY == -12 && restored.ScrollbarPositionX == 4
            && restored.ScrollbarTopAdjustment == 3 && restored.ScrollbarBottomAdjustment == -4
            && restored.ScrollbarColor == 0x40112233,
            "new appearance options survive save and normalization without altering selected values");
        Check(restored.ScrollbarOutline && restored.ScrollbarOutlineColor == 0x80334455 && restored.ScrollbarOutlineWidth == 2,
            "scrollbar outline color and thickness survive saving without losing alpha");
        Check(new Settings().ResolveScrollbarColor(0x80606060) == 0x80606060
            && new Settings { ScrollbarWhite = true }.ResolveScrollbarColor(0x80606060) == uint.MaxValue,
            "legacy scrollbar theme and white colors are preserved until explicitly edited");
        restored.CustomScrollbar = false; restored.Normalize(); restored.CustomScrollbar = true;
        Check(restored.ResolveScrollbarColor(uint.MaxValue) == 0x40112233,
            "explicit scrollbar RGB and alpha override legacy white and survive customization toggles");
        restored.ScrollbarColor = 0x00112233;
        Check(restored.ResolveScrollbarColor(uint.MaxValue) == 0x00112233,
            "zero scrollbar alpha remains transparent without forcing an opaque color");
        original.LetterSpacing = int.MinValue; original.InnerPaddingX = -10; original.InnerPaddingY = 100;
        original.BorderWidth = 100; original.WindowRounding = -10; original.ScrollbarWidth = 0;
        original.ScrollbarDisplay = 100; original.BlurStrength = float.NaN; original.OutlineStrength = float.PositiveInfinity;
        original.ScrollbarOffsetX = int.MinValue; original.ScrollbarOffsetY = int.MaxValue;
        original.ScrollbarTopAdjustment = int.MinValue; original.ScrollbarBottomAdjustment = int.MaxValue;
        original.ScrollbarOutlineWidth = int.MaxValue;
        original.Normalize();
        Check(original.LetterSpacing == -2 && original.InnerPaddingX == 0 && original.InnerPaddingY == 40
            && original.BorderWidth == 8 && original.WindowRounding == 0 && original.ScrollbarWidth == 2
            && original.ScrollbarDisplay == 2 && original.BlurStrength == 1.5f && original.OutlineStrength == 1
            && original.ScrollbarOffsetX == -200 && original.ScrollbarOffsetY == 200
            && original.ScrollbarTopAdjustment == -200 && original.ScrollbarBottomAdjustment == 200 && original.ScrollbarOutlineWidth == 4,
            "corrupt appearance values are bounded before drawing or font generation");
        var standardThumb = ScrollbarGeometry.Thumb(Vector2.Zero, 14, 100, 0, 900, 12);
        var bottomThumb = ScrollbarGeometry.Thumb(Vector2.Zero, 14, 100, 900, 900, 12);
        Check(standardThumb.First == new Vector2(3, 3) && standardThumb.Last.X == 11
            && standardThumb.ThumbHeight == 12 && bottomThumb.Last.Y == 97,
            "editable scrollbar uses standard inset grab width and minimum size at both scroll ends");
        var thinThumb = ScrollbarGeometry.Thumb(Vector2.Zero, 3, 100, 0, 900, 12);
        Check(thinThumb.Last.X - thinThumb.First.X == 3,
            "explicit thin scrollbar width remains visible within its standard track");
        var windowOrigin = new Vector2(100, 200); var windowSize = new Vector2(400, 240);
        var defaultBar = windowOrigin + new Vector2(8, 24);
        var movedLeft = ScrollbarGeometry.Place(windowOrigin, windowSize, defaultBar, 200, 3, null, -8, 0, 0);
        Check(movedLeft.Position.X == windowOrigin.X && movedLeft.Height == 200,
            "legacy negative horizontal offset reaches the window left edge");
        foreach (var offset in new[] { -200, -10, 0, 10, 200 })
        {
            var placed = ScrollbarGeometry.Place(windowOrigin, windowSize, defaultBar, 200, 3, 0, 0, offset, offset);
            Check(placed.Height >= 1 && placed.Position.X == windowOrigin.X
                && placed.Position.Y >= windowOrigin.Y && placed.Position.Y + placed.Height <= windowOrigin.Y + windowSize.Y,
                $"scrollbar endpoint adjustment at {offset}px stays bounded and never reverses the track");
        }
        foreach (var delta in new[] { -1, 1 })
        {
            var topOnly = ScrollbarGeometry.Place(windowOrigin, windowSize, defaultBar, 200, 3, 4, 0, delta, 0);
            var bottomOnly = ScrollbarGeometry.Place(windowOrigin, windowSize, defaultBar, 200, 3, 4, 0, 0, delta);
            Check(topOnly.Position.Y == defaultBar.Y - delta && topOnly.Position.Y + topOnly.Height == defaultBar.Y + 200
                && topOnly.Height == 200 + delta, $"top endpoint changes by {delta}px without moving the bottom");
            Check(bottomOnly.Position.Y == defaultBar.Y && bottomOnly.Height == 200 + delta,
                $"bottom endpoint changes by {delta}px without moving the top");
        }
        var resizedDefault = ScrollbarGeometry.Place(windowOrigin, new Vector2(400, 340), defaultBar, 300, 3, 4, 0, 2, 3);
        Check(resizedDefault.Position.Y == defaultBar.Y - 2 && resizedDefault.Height == 305,
            "endpoint adjustments follow the default viewport height after resizing");
        var farRight = ScrollbarGeometry.Place(windowOrigin, windowSize, defaultBar, 200, 3, 20000, 0, 0, 0);
        Check(farRight.Position.X + 3 == windowOrigin.X + windowSize.X,
            "absolute horizontal scrollbar position is bounded by the right window edge");
    }
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
}
