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
        ImGui.SetNextWindowViewport(viewport.ID);
        ImGui.SetNextWindowPos(layout.Position, ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        var draw = ImGui.Begin("###PcoNativeChatButtons", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.AlwaysAutoResize
            | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoBackground);
        ImGui.PopStyleVar(2);
        try
        {
            if (!draw) return;
            DrawNativeButtonRow(layout);
            NativeWindowMenu();
        }
        finally { ImGui.End(); }
    }
    private void DrawNativeButtonRow(NativeButtonLayout layout)
    {
        var buttonHeight = layout.Height;
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, buttonHeight * .5f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(layout.Padding, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(layout.Spacing, 0));
        try
        {
            ImGui.SetWindowFontScale(NativeFontScale(layout.FontSize));
            var plugin = false;
            try { plugin = ImGui.Button("Plugin##PcoNativeChat", new(0, buttonHeight)); }
            finally { ImGui.SetWindowFontScale(1); }
            if (plugin)
            {
                var windows = NativeWindows();
                if (windows.Count > 1) ImGui.OpenPopup(NativeWindowPopup);
                else if (windows.Count == 1) ToggleNativeWindow(windows[0].Id);
            }
            ImGui.SameLine();
            ImGui.PushFont(UiBuilder.IconFont);
            var gear = false;
            try
            {
                ImGui.SetWindowFontScale(NativeFontScale(layout.FontSize));
                gear = ImGui.Button(FontAwesomeIcon.Cog.ToIconString() + "##PcoNativeConfig", new(buttonHeight, buttonHeight));
            }
            finally { ImGui.SetWindowFontScale(1); ImGui.PopFont(); }
            if (gear) configOpen = !configOpen;
        }
        finally { ImGui.PopStyleVar(4); }
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
        var changed = false;
        var show = settings.ShowNativeButtons;
        if (UiCheckbox("ボタンを表示", ref show)) { settings.ShowNativeButtons = show; changed = true; }
        var size = settings.NativeButtonScale * 100;
        if (UiSlider("ボタンサイズ (%)", ref size, 50, 200)) { settings.NativeButtonScale = size / 100; changed = true; }
        var font = (int)MathF.Round(settings.NativeButtonFontSize);
        ImGui.SetNextItemWidth(180);
        if (UiInputInt("ボタンの文字サイズ (px)", ref font, 1, 4)) { settings.NativeButtonFontSize = Math.Clamp(font, 8, 32); changed = true; }
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
    private static float NativeFontScale(float fontSize)
    {
        var size = ImGui.GetFontSize();
        return size > 0 ? fontSize / size : 1;
    }
    private readonly record struct NativeWindowChoice(string Id, string Label, bool Open);
}
