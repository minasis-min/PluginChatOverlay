using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using MinasisUI;
using PluginChatOverlay;

internal static class UiChecks
{
    public static unsafe void Run()
    {
        Check(new Settings().UiStyle == OverlayUiStyle.Dalamud && JsonSerializer.Deserialize<Settings>("{\"FontSize\":23}")!.UiStyle == OverlayUiStyle.Dalamud,
            "new and existing settings default to Dalamud UI");
        var selected = new Settings { UiStyle = OverlayUiStyle.Minasis, FontSize = 24, ShowTabs = false };
        var restored = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(selected))!;
        restored.Normalize();
        Check(restored.UiStyle == OverlayUiStyle.Minasis && restored.FontSize == 24 && !restored.ShowTabs, "Minasis UI preference persists with existing font and tabs");
        restored.UiStyle = (OverlayUiStyle)99; restored.Normalize();
        Check(restored.UiStyle == OverlayUiStyle.Dalamud, "invalid UI preference safely returns to Dalamud");
        var context = ImGui.CreateContext();
        try
        {
            var io = ImGui.GetIO(); io.IniFilename = null; io.LogFilename = null;
            io.DisplaySize = new(1600, 1200); io.DeltaTime = 1f / 60;
            io.Fonts.AddFontDefault();
            var shortcutFont = io.Fonts.AddFontDefault();
            byte* pixels; int w, h; io.Fonts.GetTexDataAsRGBA32(0, &pixels, &w, &h);
            var shortcut = typeof(Plugin).GetMethod("DrawNativeShortcut", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            foreach (var iconSize in new[] { 8f, 18f, 32f })
            {
                ImGui.NewFrame();
                ImGui.Begin("Shortcut font isolation");
                try
                {
                    ImGui.SetWindowFontScale(1.4f);
                    var hostFont = ImGui.GetFont();
                    var hostSize = ImGui.GetFontSize();
                    shortcut.Invoke(null, ["shortcut", "A", new NativeButtonLayout(Vector2.Zero, 32, iconSize, 4), shortcutFont]);
                    Check(ImGui.GetFont().Equals(hostFont) && ImGui.GetFontSize() == hostSize,
                        $"shortcut explicit font at {iconSize}px preserves host font and scale");
                    // A caller's font scope must also remain intact across shortcut drawing.
                    ImGui.PushFont(shortcutFont);
                    try
                    {
                        var scopedSize = ImGui.GetFontSize();
                        shortcut.Invoke(null, ["scoped-shortcut", "A", new NativeButtonLayout(Vector2.Zero, 32, iconSize, 4), hostFont]);
                        Check(ImGui.GetFont().Equals(shortcutFont) && ImGui.GetFontSize() == scopedSize,
                            $"shortcut at {iconSize}px preserves caller font scope");
                    }
                    finally { ImGui.PopFont(); }
                    Check(ImGui.GetFont().Equals(hostFont), $"shortcut at {iconSize}px leaves font stack balanced");
                }
                finally { ImGui.End(); }
                ImGui.Render();
            }
            var beginShortcuts = typeof(Plugin).GetMethod("BeginNativeButtonWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            foreach (var height in new[] { 32f, 18.199f, 64f, 12f, 24.266f })
            {
                ImGui.NewFrame();
                var layout = new NativeButtonLayout(new(200, 200), height, 18, 4);
                var paddingBefore = ImGui.GetStyle().WindowPadding;
                beginShortcuts.Invoke(null, [layout, ImGui.GetMainViewport().ID]);
                try
                {
                    var clipMin = ImGui.GetWindowDrawList().GetClipRectMin();
                    var clipMax = ImGui.GetWindowDrawList().GetClipRectMax();
                    shortcut.Invoke(null, ["first", "A", layout, shortcutFont]);
                    var firstMin = ImGui.GetItemRectMin();
                    var firstMax = ImGui.GetItemRectMax();
                    ImGui.SameLine(0, layout.Spacing);
                    shortcut.Invoke(null, ["second", "A", layout, shortcutFont]);
                    var lastMax = ImGui.GetItemRectMax();
                    Check(firstMin.X >= clipMin.X + 1 && firstMin.Y >= clipMin.Y + 1
                        && firstMax.Y <= clipMax.Y - 1 && lastMax.X <= clipMax.X - 1 && lastMax.Y <= clipMax.Y - 1,
                        $"native shortcut resize to {height}px keeps both circles inside clip bounds immediately");
                    Check(ImGui.GetStyle().WindowPadding == paddingBefore, $"native shortcut window at {height}px restores style");
                }
                finally { ImGui.End(); }
                ImGui.Render();
            }
            var appearancePlugin = (Plugin)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
            var barSettings = new Settings { AppearanceMode = ChatAppearanceMode.Custom, BackgroundMode = ChatBackgroundMode.Darken, ScrollbarWidth = 3, ScrollbarWhite = true, ScrollbarDisplay = 2 };
            const System.Reflection.BindingFlags privateInstance = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(Plugin).GetField("settings", privateInstance)!.SetValue(appearancePlugin, barSettings);
            var viewType = typeof(Plugin).GetNestedType("ViewState", System.Reflection.BindingFlags.NonPublic)!;
            var barView = Activator.CreateInstance(viewType, true)!;
            var drawBar = typeof(Plugin).GetMethod("DrawChatScrollbar", privateInstance)!;
            foreach (var scroll in new[] { -100f, 500f, 2000f })
            {
                ImGui.NewFrame(); ImGui.Begin("Scrollbar bounds");
                try
                {
                    var pos = ImGui.GetCursorScreenPos();
                    var list = ImGui.GetWindowDrawList();
                    var firstVertex = list.VtxBuffer.Size;
                    drawBar.Invoke(appearancePlugin, ["bar-test", barView, pos, 100f, scroll, 1000f, true, 1d]);
                    var vertices = Enumerable.Range(firstVertex, list.VtxBuffer.Size - firstVertex).Select(i => list.VtxBuffer[i].Pos).ToArray();
                    Check(vertices.Length > 0 && vertices.All(v => v.X >= pos.X - 1 && v.X <= pos.X + 4
                        && v.Y >= pos.Y - 1 && v.Y <= pos.Y + 101),
                        $"thin white scrollbar remains within bounds for scroll {scroll}, including trimmed history");
                    barSettings.ScrollbarOutline = true;
                    firstVertex = list.VtxBuffer.Size;
                    drawBar.Invoke(appearancePlugin, ["bar-outline-test", barView, pos, 100f, scroll, 1000f, true, 1d]);
                    var outlinedCount = list.VtxBuffer.Size - firstVertex;
                    var boundedOutline = true; var yellowFound = false; var exteriorGlow = false; var glowAlphas = new HashSet<uint>();
                    for (var v = firstVertex; v < list.VtxBuffer.Size; v++)
                    {
                        var vertex = list.VtxBuffer[v];
                        boundedOutline &= vertex.Pos.X >= pos.X - barSettings.ScrollbarOutlineWidth - 1 && vertex.Pos.X <= pos.X + 4 + barSettings.ScrollbarOutlineWidth
                            && vertex.Pos.Y >= pos.Y - barSettings.ScrollbarOutlineWidth - 1 && vertex.Pos.Y <= pos.Y + 101 + barSettings.ScrollbarOutlineWidth;
                        if ((vertex.Col & 0x00FFFFFFu) == (barSettings.ScrollbarOutlineColor & 0x00FFFFFFu)) { yellowFound = true; exteriorGlow |= vertex.Pos.X < pos.X || vertex.Pos.X > pos.X + 3; if ((vertex.Col >> 24) != 0) glowAlphas.Add(vertex.Col >> 24); }
                    }
                    Check(outlinedCount > vertices.Length && boundedOutline && yellowFound && exteriorGlow && glowAlphas.Count > 1,
                        $"scrollbar outer glow extends outside thumb with fading yellow geometry at {scroll}");
                    barSettings.ScrollbarColor = 0x00FFFFFF;
                    firstVertex = list.VtxBuffer.Size;
                    drawBar.Invoke(appearancePlugin, ["bar-transparent-test", barView, pos, 100f, scroll, 1000f, true, 1d]);
                    Check(list.VtxBuffer.Size == firstVertex, "zero scrollbar alpha also hides the outer glow");
                    barSettings.ScrollbarColor = null; barSettings.ScrollbarOutline = false;
                    barSettings.ScrollbarDisplay = 1;
                    firstVertex = list.VtxBuffer.Size;
                    drawBar.Invoke(appearancePlugin, ["bar-hidden-test", barView, pos, 100f, scroll, 1000f, true, 1d]);
                    Check(list.VtxBuffer.Size == firstVertex, "automatic scrollbar emits no geometry when not scrolling or hovered");
                    barSettings.ScrollbarDisplay = 2;
                }
                finally { ImGui.End(); }
                ImGui.Render();
            }
            var appearanceViews = typeof(Plugin).GetField("views", privateInstance)!;
            var updateBar = typeof(Plugin).GetMethod("UpdateScrollbarInput", privateInstance)!;
            viewType.GetField("ScrollMaximum")!.SetValue(barView, 900f);
            viewType.GetField("PreviousScroll")!.SetValue(barView, 100f);
            float pressedScroll = 0;
            for (var frame = 0; frame < 5; frame++)
            {
                io.AddMousePosEvent(181.5f, frame == 3 ? 230 : 165);
                io.AddMouseButtonEvent(0, frame is 2 or 3);
                ImGui.NewFrame();
                ImGui.SetNextWindowPos(new(100, 100), ImGuiCond.Always);
                ImGui.SetNextWindowSize(new(400, 250), ImGuiCond.Always);
                ImGui.Begin("Scrollbar-over-child", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings);
                try
                {
                    if (frame == 2) ((TextSelection)viewType.GetField("Selection")!.GetValue(barView)!).Active = true;
                    updateBar.Invoke(appearancePlugin, [barView, new Vector2(180, 140), 140f, false, 1d]);
                    if (frame == 2)
                    {
                        pressedScroll = (float)viewType.GetField("RequestedScroll")!.GetValue(barView)!;
                        Check((bool)viewType.GetField("ScrollbarHovered")!.GetValue(barView)!
                            && (bool)viewType.GetField("ScrollbarDragging")!.GetValue(barView)!,
                            "scrollbar click over the history child starts a root-owned drag");
                        Check(!((TextSelection)viewType.GetField("Selection")!.GetValue(barView)!).Active,
                            "scrollbar input cancels text drag before the history processes the same click");
                    }
                    if (frame == 3) Check((float)viewType.GetField("RequestedScroll")!.GetValue(barView)! > pressedScroll,
                        "scrollbar drag over the child requests an increasing scroll position");
                    if (frame == 4) Check(!(bool)viewType.GetField("ScrollbarDragging")!.GetValue(barView)!,
                        "scrollbar release stops the drag without relying on a parent InvisibleButton");
                    ImGui.BeginChild("History-input", new Vector2(350, 160), false);
                    ImGui.Dummy(new(300, 600)); ImGui.EndChild();
                }
                finally { ImGui.End(); }
                ImGui.Render();
            }
            viewType.GetField("RequestedScroll")!.SetValue(barView, null);
            updateBar.Invoke(appearancePlugin, [barView, new Vector2(180, 140), 140f, true, 1d]);
            Check(!(bool)viewType.GetField("ScrollbarHovered")!.GetValue(barView)!
                && !(bool)viewType.GetField("ScrollbarDragging")!.GetValue(barView)!
                && viewType.GetField("RequestedScroll")!.GetValue(barView) == null,
                "clickthrough scrollbar does not consume mouse input or request scrolling");
            io.AddMousePosEvent(-1000, -1000); io.AddMouseButtonEvent(0, false);
            appearanceViews.SetValue(appearancePlugin, Activator.CreateInstance(appearanceViews.FieldType));
            var chatWindowType = typeof(Plugin).GetNestedType("ChatWindow", System.Reflection.BindingFlags.NonPublic)!;
            var chatWindow = (Dalamud.Interface.Windowing.Window)Activator.CreateInstance(chatWindowType,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null, [appearancePlugin, "main", null], null)!;
            ImGui.NewFrame();
            var legacyPadding = ImGui.GetStyle().WindowPadding;
            var legacyBorder = ImGui.GetStyle().WindowBorderSize;
            var legacyRounding = ImGui.GetStyle().WindowRounding;
            var legacyColor = ImGui.GetStyle().Colors[(int)ImGuiCol.Border];
            var legacyBackground = ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg];
            chatWindow.PreDraw();
            Check(barSettings.WindowLayoutInitialized && barSettings.InnerPaddingX == MathF.Round(legacyPadding.X)
                && barSettings.InnerPaddingY == MathF.Round(legacyPadding.Y) && barSettings.BorderWidth == MathF.Round(legacyBorder)
                && barSettings.WindowRounding == MathF.Round(legacyRounding)
                && barSettings.BorderColor == ImGui.ColorConvertFloat4ToU32(legacyColor),
                "custom layout initial values are captured from the active host defaults");
            Check(ImGui.GetStyle().WindowPadding == legacyPadding && ImGui.GetStyle().WindowBorderSize == legacyBorder
                && ImGui.GetStyle().WindowRounding == legacyRounding && ImGui.GetStyle().Colors[(int)ImGuiCol.Border] == legacyColor,
                "default chat layout retains the original host padding, border and rounding");
            Check(chatWindow.Flags.HasFlag(ImGuiWindowFlags.NoScrollbar) && chatWindow.Flags.HasFlag(ImGuiWindowFlags.NoScrollWithMouse),
                "outer chat window cannot create a second scrollbar or consume history wheel scrolling");
            Check(ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg] == new Vector4(0, 0, 0, 1),
                "chat background uses black alpha for multiplication without a framebuffer capture");
            ImGui.Begin("Legacy style"); ImGui.End(); chatWindow.PostDraw();
            Check(ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg] == legacyBackground,
                "multiplicative chat background restores the shared host background after drawing");
            ImGui.Render();
            barSettings.CustomWindowLayout = true;
            foreach (var pad in new[] { 0, 12, 40 })
            {
                barSettings.InnerPaddingX = pad; barSettings.InnerPaddingY = pad;
                barSettings.BorderWidth = pad == 0 ? 0 : 3; barSettings.WindowRounding = pad == 0 ? 0 : 12;
                ImGui.NewFrame();
                var beforePadding = ImGui.GetStyle().WindowPadding;
                var beforeBorder = ImGui.GetStyle().WindowBorderSize;
                var beforeRounding = ImGui.GetStyle().WindowRounding;
                var beforeColor = ImGui.GetStyle().Colors[(int)ImGuiCol.Border];
                chatWindow.PreDraw();
                Check(ImGui.GetStyle().WindowPadding == new Vector2(pad) && ImGui.GetStyle().WindowBorderSize == barSettings.BorderWidth,
                    $"chat window applies padding and border at {pad}px");
                ImGui.Begin("Chat style restoration"); ImGui.End();
                chatWindow.PostDraw();
                Check(ImGui.GetStyle().WindowPadding == beforePadding && ImGui.GetStyle().WindowBorderSize == beforeBorder
                    && ImGui.GetStyle().WindowRounding == beforeRounding && ImGui.GetStyle().Colors[(int)ImGuiCol.Border] == beforeColor,
                    $"chat window appearance restores shared styles at {pad}px");
                ImGui.Render();
            }
            ImGui.NewFrame();
            barSettings.CustomWindowLayout = false;
            typeof(Plugin).GetMethod("EnsureWindowLayoutDefaults", privateInstance)!.Invoke(appearancePlugin, null);
            Check(barSettings.InnerPaddingX == 40 && barSettings.BorderWidth == 3 && barSettings.WindowRounding == 12,
                "disabling and initializing layout again preserves edited values");
            typeof(Plugin).GetMethod("ResetWindowLayoutDefaults", privateInstance)!.Invoke(appearancePlugin, null);
            Check(barSettings.InnerPaddingX == MathF.Round(ImGui.GetStyle().WindowPadding.X)
                && barSettings.BorderColor == ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)ImGuiCol.Border]),
                "layout reset returns to current host defaults without enabling customization");
            barSettings.BackgroundBlur = true;
            barSettings.CustomWindowLayout = true; barSettings.BorderWidth = 3; barSettings.BorderColor = 0xFF2211AA;
            var borderBeforeSoft = ImGui.GetStyle().Colors[(int)ImGuiCol.Border];
            chatWindow.PreDraw();
            Check((uint)chatWindowType.GetField("softBorderColor", privateInstance)!.GetValue(chatWindow)! == barSettings.BorderColor
                && (float)chatWindowType.GetField("softBorderWidth", privateInstance)!.GetValue(chatWindow)! == 3,
                "soft-edge window captures custom border color and thickness before suppressing the hard border");
            ImGui.Begin("Soft edge style");
            var softDraw = ImGui.GetWindowDrawList(); var softOrigin = ImGui.GetCursorScreenPos();
            var firstSoftVertex = softDraw.VtxBuffer.Size;
            SoftWindowBackground.Draw(softDraw, softOrigin, softOrigin + new Vector2(100, 50), 0xFF202020, .8f, 5, 4);
            var bounded = true; var exteriorHalo = false; var lowAlpha = 255u; var highAlpha = 0u;
            for (var v = firstSoftVertex; v < softDraw.VtxBuffer.Size; v++)
            {
                var vertex = softDraw.VtxBuffer[v];
                bounded &= vertex.Pos.X >= softOrigin.X - 16 && vertex.Pos.X <= softOrigin.X + 116
                    && vertex.Pos.Y >= softOrigin.Y - 16 && vertex.Pos.Y <= softOrigin.Y + 66;
                exteriorHalo |= (vertex.Col >> 24) != 0 && (vertex.Pos.X < softOrigin.X || vertex.Pos.X > softOrigin.X + 100);
                lowAlpha = Math.Min(lowAlpha, vertex.Col >> 24); highAlpha = Math.Max(highAlpha, vertex.Col >> 24);
            }
            Check(bounded && exteriorHalo && highAlpha > lowAlpha && softDraw.VtxBuffer.Size > firstSoftVertex,
                "Gaussian soft background fades through a bounded exterior halo");
            Check(Math.Abs(SoftWindowBackground.Coverage(0, 5) - .5f) < .001f
                && SoftWindowBackground.Coverage(-15, 5) == 0 && SoftWindowBackground.Coverage(15, 5) == 1,
                "Gaussian edge has half opacity at the boundary and finite three-sigma tails");
            Check(Enumerable.Range(-14, 28).All(i => SoftWindowBackground.Coverage(i, 5) <= SoftWindowBackground.Coverage(i + 1, 5))
                && Math.Abs(SoftWindowBackground.Coverage(-5, 5) + SoftWindowBackground.Coverage(5, 5) - 1) < .001f,
                "Gaussian edge coverage is monotonic and symmetric across the boundary");
            var beforeTransparent = softDraw.VtxBuffer.Size;
            SoftWindowBackground.Draw(softDraw, softOrigin, softOrigin + new Vector2(100, 50), 0u, 0, 5, 4);
            Check(softDraw.VtxBuffer.Size == beforeTransparent, "transparent background skips soft edge drawing");
            var firstBorder = softDraw.VtxBuffer.Size;
            SoftWindowBackground.DrawBorder(softDraw, softOrigin, softOrigin + new Vector2(100, 50), 0xFF2211AA, 3, 2, 4);
            var coloredBorder = false; var borderBounded = true;
            for (var v = firstBorder; v < softDraw.VtxBuffer.Size; v++)
            {
                var vertex = softDraw.VtxBuffer[v]; coloredBorder |= (vertex.Col & 0x00FFFFFFu) == 0x002211AA;
                borderBounded &= vertex.Pos.X >= softOrigin.X - 16 && vertex.Pos.X <= softOrigin.X + 116
                    && vertex.Pos.Y >= softOrigin.Y - 16 && vertex.Pos.Y <= softOrigin.Y + 66;
            }
            Check(coloredBorder && borderBounded, "Gaussian border honors configured RGB within its bounded halo");
            var zeroBorder = softDraw.VtxBuffer.Size;
            SoftWindowBackground.DrawBorder(softDraw, softOrigin, softOrigin + new Vector2(100, 50), 0xFF2211AA, 0, 2, 4);
            Check(softDraw.VtxBuffer.Size == zeroBorder, "zero configured border thickness suppresses the soft outline");
            ImGui.End(); chatWindow.PostDraw();
            Check(ImGui.GetStyle().Colors[(int)ImGuiCol.Border] == borderBeforeSoft,
                "soft edge border override restores the shared host style");
            ImGui.Render(); barSettings.BackgroundBlur = false;
            using (var appearanceUi = new Ui())
            foreach (var minasis in new[] { false, true })
            {
                ImGui.NewFrame();
                using (minasis ? new OverlayThemeScope(appearanceUi) : null)
                {
                    var expectedBackground = ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg];
                    var expectedPadding = ImGui.GetStyle().WindowPadding;
                    var expectedRounding = ImGui.GetStyle().WindowRounding;
                    var expectedBorder = ImGui.GetStyle().WindowBorderSize;
                    barSettings.AppearanceMode = ChatAppearanceMode.Standard;
                    barSettings.BackgroundBlur = true; barSettings.CustomWindowLayout = true;
                    chatWindow.PreDraw();
                    try
                    {
                        Check(ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg] == expectedBackground
                            && ImGui.GetStyle().WindowPadding == expectedPadding && ImGui.GetStyle().WindowRounding == expectedRounding
                            && ImGui.GetStyle().WindowBorderSize == expectedBorder && chatWindow.AllowBackgroundBlur,
                            $"standard appearance restores {(minasis ? "Minasis" : "Dalamud")} theme geometry and background while custom values are retained");
                        ImGui.Begin("Standard appearance check"); ImGui.End();
                    }
                    finally { chatWindow.PostDraw(); }
                    barSettings.AppearanceMode = ChatAppearanceMode.Custom;
                    foreach (var background in new[] { ChatBackgroundMode.Normal, ChatBackgroundMode.Darken })
                    {
                        barSettings.BackgroundMode = background;
                        chatWindow.PreDraw();
                        try
                        {
                            Check(ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg] == (background == ChatBackgroundMode.Normal ? SnapshotColor(barSettings.CustomChatStyle!, ImGuiCol.WindowBg) : new Vector4(0, 0, 0, 1))
                                && !chatWindow.AllowBackgroundBlur,
                                $"custom {background} background respects {(minasis ? "Minasis" : "Dalamud")} captured custom background and retained edge blur");
                            ImGui.Begin("Custom appearance check"); ImGui.End();
                        }
                        finally { chatWindow.PostDraw(); }
                        Check(ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg] == expectedBackground
                            && ImGui.GetStyle().WindowPadding == expectedPadding && ImGui.GetStyle().WindowBorderSize == expectedBorder,
                            $"{background} appearance restores the caller theme after drawing");
                    }
                }
                ImGui.Render();
            }
            ImGui.NewFrame(); ImGui.Begin("Popup style isolation");
            var normalMenu = ChatPopupStyle.Capture();
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 30));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 22);
            ImGui.PushStyleColor(ImGuiCol.Border, 0u);
            ImGui.PushStyleColor(ImGuiCol.ChildBg, 0u);
            ImGui.PushFont(shortcutFont);
            try
            {
                using (new ChatPopupStyle.Scope(normalMenu, true))
                {
                    ImGui.OpenPopup("style-check");
                    if (ImGui.BeginPopup("style-check"))
                    {
                        try
                        {
                            Check(ImGui.GetStyle().WindowPadding == normalMenu.Padding
                                && ImGui.GetStyle().WindowBorderSize == normalMenu.BorderWidth
                                && ImGui.GetStyle().WindowRounding == normalMenu.Rounding
                                && ImGui.GetStyle().Colors[(int)ImGuiCol.Border] == normalMenu.Border
                                && ImGui.GetFont().Equals(normalMenu.Font),
                                "chat context popup restores selected UI padding, border, rounding and font before BeginPopup");
                            ImGui.CloseCurrentPopup();
                        }
                        finally { ImGui.EndPopup(); }
                    }
                }
                Check(ImGui.GetStyle().WindowPadding == new Vector2(0, 30)
                    && ImGui.GetStyle().Colors[(int)ImGuiCol.Border].W == 0 && ImGui.GetFont().Equals(shortcutFont),
                    "popup style scope restores customized chat styles and font after menu drawing");
                try { using var exceptionalScope = new ChatPopupStyle.Scope(normalMenu, true); throw new InvalidOperationException(); }
                catch (InvalidOperationException) { }
                Check(ImGui.GetStyle().WindowRounding == 22 && ImGui.GetFont().Equals(shortcutFont),
                    "popup style scope restores chat drawing state after an exception");
            }
            finally { ImGui.PopFont(); ImGui.PopStyleColor(2); ImGui.PopStyleVar(3); ImGui.End(); }
            ImGui.Render();
            using var ui = new Ui(); using var other = new Ui { Theme = UiTheme.Cream };
            foreach (var scale in new[] { 1f, 1.5f, 2f })
            {
                ui.Scale = scale;
                ImGui.NewFrame();
                var hostGap = ImGui.GetStyle().ItemSpacing.Y;
                var customFromHost = new Settings { AppearanceMode = ChatAppearanceMode.Custom };
                customFromHost.CaptureCustomMessageGap(hostGap, scale);
                var customFromMinasis = new Settings { AppearanceMode = ChatAppearanceMode.Custom };
                float minasisGap;
                using (new OverlayThemeScope(ui))
                {
                    minasisGap = ImGui.GetStyle().ItemSpacing.Y;
                    customFromMinasis.CaptureCustomMessageGap(minasisGap, scale);
                    Check(Math.Abs(customFromHost.ResolveMessageGap(minasisGap, scale) - hostGap) < .001f,
                        $"custom messages retain the captured Dalamud gap under Minasis at {scale} scale");
                    Check(new Settings().ResolveMessageGap(minasisGap, scale) == minasisGap,
                        $"standard messages retain the previous Minasis theme spacing at {scale} scale");
                }
                Check(Math.Abs(customFromMinasis.ResolveMessageGap(hostGap, scale) - minasisGap) < .001f
                    && ImGui.GetStyle().ItemSpacing.Y == hostGap,
                    $"custom Minasis spacing survives a return to Dalamud without modifying shared styles at {scale} scale");
                ImGui.Render();
            }
            var flag = true; var integer = 17; var number = 80f; var text = "Tab 1"; var index = 1;
            UiChoice[] choices = [new("native", "Dalamud"), new("minasis", "MinasisUI")];
            foreach (var scale in new[] { 1f, 1.5f, 2f })
            {
                ui.Scale = scale;
                var padding = ImGui.GetStyle().WindowPadding; var color = ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
                var tabColor = ImGui.GetStyle().Colors[(int)ImGuiCol.TabActive];
                ImGui.NewFrame();
                using (new OverlayThemeScope(ui))
                {
                    ImGui.SetNextWindowSize(new(900, 950));
                    var themedMenu = ChatPopupStyle.Capture();
                    ImGui.PushStyleColor(ImGuiCol.Border, 0u);
                    ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
                    try
                    {
                        using var menuScope = new ChatPopupStyle.Scope(themedMenu, true);
                        Check(ImGui.GetStyle().Colors[(int)ImGuiCol.Border] == themedMenu.Border
                            && ImGui.GetStyle().WindowPadding == themedMenu.Padding,
                            $"popup appearance preserves the selected Minasis theme at {scale} scale");
                    }
                    finally { ImGui.PopStyleVar(); ImGui.PopStyleColor(); }
                    ImGui.Begin("Settings###PCO-check");
                    try
                    {
                        if (ImGui.BeginTabBar("SettingsTabs"))
                        {
                            try
                            {
                                if (ImGui.BeginTabItem("Basic"))
                                {
                                    try
                                    {
                                        ui.Heading("Appearance"); ui.Dropdown("ui", "UI", ref index, choices);
                                        ui.Checkbox("enabled", "Capture", ref flag);
                                        ui.InputInt("font", "Font px", ref integer);
                                        ui.Slider("alpha", "Background", ref number, 0, 100, "%.0f");
                                        ui.InputText("name", "Tab name", ref text, 48); ui.Button("apply", "Apply");
                                    }
                                    finally { ImGui.EndTabItem(); }
                                }
                            }
                            finally { ImGui.EndTabBar(); }
                        }
                    }
                    finally { ImGui.End(); }
                }
                ImGui.Render();
                Check(ImGui.GetStyle().WindowPadding == padding && ImGui.GetStyle().Colors[(int)ImGuiCol.Text] == color
                    && ImGui.GetStyle().Colors[(int)ImGuiCol.TabActive] == tabColor && ImGui.GetDrawData().TotalVtxCount > 0,
                    $"Minasis native controls render at {scale} scale and restore host text, tabs and dimensions");
                ImGui.NewFrame();
                var left = Vector2.Zero; var right = Vector2.Zero; var fontRestored = false;
                using (new OverlayThemeScope(ui))
                {
                    ImGui.SetNextWindowSize(new(740, 540)); ui.ConstrainNextTwoColumnWindow();
                    ImGui.Begin("Sidebar###PCO-sidebar-check");
                    try
                    {
                        ui.TwoColumn("layout", () =>
                        {
                            left = ImGui.GetWindowPos(); var font = ImGui.GetFont();
                            ui.NavigationItem("basic", "Basic", true, "*", font);
                            ui.NavigationItem("history", "History", false, "+", font);
                            fontRestored = ImGui.GetFont().Handle == font.Handle;
                        }, () => { right = ImGui.GetWindowPos(); ui.Heading("Settings"); });
                    }
                    finally { ImGui.End(); }
                }
                ImGui.Render();
                Check(right.X > left.X && Math.Abs(right.Y - left.Y) < 1 && fontRestored
                    && ImGui.GetStyle().WindowPadding == padding,
                    $"icon sidebar stays left at {scale} scale and restores host font and layout");
            }
            ImGui.NewFrame();
            var before = ImGui.GetStyle().WindowPadding;
            try { using var scope = new OverlayThemeScope(ui); throw new IOException("consumer failure"); }
            catch (IOException) { }
            Check(ImGui.GetStyle().WindowPadding == before, "Minasis overlay theme unwinds on consumer exception");
            var duplicate = new OverlayThemeScope(ui); duplicate.Dispose(); duplicate.Dispose();
            Check(ImGui.GetStyle().WindowPadding == before, "Minasis overlay theme tolerates repeated disposal");
            ImGui.EndFrame();
            ui.Dispose();
            Check(other.Theme == UiTheme.Cream, "disposing embedded UI preserves other instance state");
        }
        finally { ImGui.DestroyContext(context); }
    }
    private static Vector4 SnapshotColor(ChatStyleSnapshot snapshot, ImGuiCol color)
    { var i = (int)color * 4; return new(snapshot.Colors[i], snapshot.Colors[i + 1], snapshot.Colors[i + 2], snapshot.Colors[i + 3]); }
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
}
