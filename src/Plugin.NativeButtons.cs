using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace PluginChatOverlay;

// Buttons placed to the right of the native chat log close control.
public sealed partial class Plugin
{
    private const uint ChatLogCloseNodeId = 11;
    private const string NativeWindowPopup = "PcoNativeWindows";

    private unsafe void DrawNativeChatButtons()
    {
        if (!settings.ShowNativeButtons) return;
        var addon = (AtkUnitBase*)game.GetAddonByName("ChatLog", 1).Address;
        if (addon == null || !addon->IsVisible) return;
        var node = addon->GetNodeById(ChatLogCloseNodeId);
        if (node == null || !node->IsVisible()) return;
        var scaleX = node->GetScaleX();
        var scaleY = node->GetScaleY();
        var width = node->GetWidth() * scaleX;
        var height = node->GetHeight() * scaleY;
        if (width <= 0 || height <= 0) return;
        var viewport = ImGui.GetMainViewport();
        var layout = NativeButtonLayout.Create(new(node->ScreenX, node->ScreenY, width, height), viewport.Pos,
            settings.NativeButtonScale, settings.NativeButtonFontSize, Math.Max(ImGuiHelpers.GlobalScale, scaleY),
            new(settings.NativeButtonOffsetX, settings.NativeButtonOffsetY));
        var draw = BeginNativeButtonWindow(layout, viewport.ID);
        try
        {
            if (!draw) return;
            DrawNativeButtonRow(layout);
            NativeWindowMenu();
        }
        finally { ImGui.End(); }
    }
    private static bool BeginNativeButtonWindow(NativeButtonLayout layout, uint viewportId)
    {
        const float edge = 2;
        ImGui.SetNextWindowViewport(viewportId);
        ImGui.SetNextWindowPos(layout.Position - new Vector2(edge), ImGuiCond.Always);
        // Auto-resize uses prior frame content dimensions. Explicit sizing avoids
        // clipping while sliders change the diameter, including fractional sizes.
        ImGui.SetNextWindowSize(new(MathF.Ceiling(layout.Height * 2 + layout.Spacing + edge * 2),
            MathF.Ceiling(layout.Height + edge * 2)), ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(edge));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, Vector2.Zero);
        try
        {
            return ImGui.Begin("###PcoNativeChatButtons", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove
                | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav
                | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        }
        finally { ImGui.PopStyleVar(3); }
    }
    private void DrawNativeButtonRow(NativeButtonLayout layout)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(layout.Spacing, 0));

        try
        {
            if (DrawNativeIcon("PcoNativeChat", FontAwesomeIcon.Comments, "チャット表示/非表示", layout))
            {
                var windows = NativeWindows();
                if (windows.Count > 1) ImGui.OpenPopup(NativeWindowPopup);
                else if (windows.Count == 1) ToggleNativeWindow(windows[0].Id);
            }
            ImGui.SameLine();
            if (DrawNativeIcon("PcoNativeConfig", FontAwesomeIcon.Cog, "設定", layout))
                configOpen = !configOpen;
        }
        finally { ImGui.PopStyleVar(); }
    }
    private static bool DrawNativeIcon(string id, FontAwesomeIcon icon, string description, NativeButtonLayout layout)
    {
        var clicked = DrawNativeShortcut(id, icon.ToIconString(), layout, UiBuilder.IconFont);
        if (ImGui.IsItemHovered())
        {
            var bounds = ImGui.GetItemRectMin();
            ImGui.SetNextWindowPos(new(bounds.X + layout.Height / 2, bounds.Y - 4), ImGuiCond.Always, new(.5f, 1));
            ImGui.BeginTooltip();
            try { ImGui.TextUnformatted(description); }
            finally { ImGui.EndTooltip(); }
        }
        return clicked;
    }
    private static unsafe bool DrawNativeShortcut(string id, string label, NativeButtonLayout layout, ImFontPtr font)
    {
        // Explicit draw-list font: do not touch the shared ImGui font stack or window font scale.
        var fontSize = Math.Min(layout.FontSize, layout.Height * .62f);
        var size = new Vector2(layout.Height, layout.Height);
        var position = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(id, size);
        var hovered = ImGui.IsItemHovered();
        var pressed = ImGui.IsItemActive();
        var drawList = ImGui.GetWindowDrawList();
        var fill = pressed ? 0xFF44484B : hovered ? 0xFF74797D : 0xFF565B5F;
        drawList.AddRectFilled(position, position + size, fill, layout.Height / 2);
        drawList.AddRect(position, position + size, 0xFFA0A5A8, layout.Height / 2);
        if (font.IsNull || font.FontSize <= 0 || label.Length == 0) return clicked;
        var glyph = font.FindGlyph(label[0]);
        if (glyph == null) return clicked;
        var scale = fontSize / font.FontSize;
        var inkCenter = new Vector2((glyph->X0 + glyph->X1) / 2, (glyph->Y0 + glyph->Y1) / 2) * scale;
        var textPosition = position + size / 2 - inkCenter;
        drawList.AddText(font, fontSize, textPosition + new Vector2(0, 1), 0xB0000000, label);
        drawList.AddText(font, fontSize, textPosition, 0xFFE6E6E6, label);
        return clicked;
    }
    private void NativeWindowMenu()
    {
        if (!ImGui.BeginPopup(NativeWindowPopup)) return;
        try
        {
            foreach (var window in NativeWindows())
                if (ImGui.MenuItem(window.Label, "", window.Open)) ToggleNativeWindow(window.Id);
        }
        finally { ImGui.EndPopup(); }
    }
    private List<NativeWindowChoice> NativeWindows()
    {
        var windows = new List<NativeWindowChoice>();
        if (settings.WindowTabs("main").Any()) windows.Add(new("main", "メイン###native-main", settings.IsWindowVisible("main")));
        foreach (var tab in settings.Tabs)
            if (tab.Detached) windows.Add(new(tab.Id, tab.Name + "###native-" + tab.Id, settings.IsWindowVisible(tab.Id)));
        return windows;
    }
    private void ToggleNativeWindow(string id)
    {
        if (settings.ToggleWindowVisibility(id)) Save();
    }
    private bool DrawNativeButtonSettings()
    {
        UiSection("ショートカット");
        ImGui.TextWrapped("吹き出し：チャット表示/非表示。複数ウィンドウがある場合は、切り替えるウィンドウを選びます。");
        ImGui.TextWrapped("歯車：設定。クリックすると設定画面を開く・閉じることができます。");
        var changed = false;
        var show = settings.ShowNativeButtons;
        if (UiCheckbox("ボタンを表示", ref show)) { settings.ShowNativeButtons = show; changed = true; }
        var size = settings.NativeButtonScale * 100;
        if (UiSlider("ボタンサイズ (%)", ref size, 50, 200)) { settings.NativeButtonScale = size / 100; changed = true; }
        var font = (int)MathF.Round(settings.NativeButtonFontSize);
        ImGui.SetNextItemWidth(180);
        if (UiInputInt("ボタンのアイコンサイズ (px)", ref font, 1, 4)) { settings.NativeButtonFontSize = Math.Clamp(font, 8, 32); changed = true; }
        var x = settings.NativeButtonOffsetX;
        if (NativeOffsetInput("native-offset-x", "横位置調整 (px)", ref x)) { settings.NativeButtonOffsetX = x; changed = true; }
        var y = settings.NativeButtonOffsetY;
        if (NativeOffsetInput("native-offset-y", "縦位置調整 (px)", ref y)) { settings.NativeButtonOffsetY = y; changed = true; }
        if (UiButton("ボタン位置を標準に戻す"))
        {
            settings.NativeButtonOffsetX = settings.NativeButtonOffsetY = 0;
            changed = true;
        }
        ImGui.TextWrapped("横は＋で右、−で左。縦は＋で下、−で上へ移動します。1px刻みで調整でき、UI倍率に関係なく指定したpx分だけ移動します。");
        return changed;
    }
    private bool NativeOffsetInput(string id, string label, ref int value)
    {
        ImGui.SetNextItemWidth(180 * ImGuiHelpers.GlobalScale);
        var changed = minasisFrame ? ui.InputInt(id, label, ref value, 1, true, -2000, 2000)
            : ImGui.InputInt(label, ref value, 1, 10);
        if (changed) value = Math.Clamp(value, -2000, 2000);
        return changed;
    }
    private readonly record struct NativeWindowChoice(string Id, string Label, bool Open);
}
