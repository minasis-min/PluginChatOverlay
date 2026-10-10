using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
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
        var width = node->GetWidth() * node->ScaleX;
        var height = node->GetHeight() * node->GetScaleY();
        if (width <= 0 || height <= 0) return;
        var scale = settings.NativeButtonScale;
        var buttonHeight = Math.Max(height * scale, settings.NativeButtonFontSize);
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowViewport(viewport.ID);
        ImGui.SetNextWindowPos(new(viewport.Pos.X + node->ScreenX + width + 4 * scale, viewport.Pos.Y + node->ScreenY + (height - buttonHeight) / 2), ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        var draw = ImGui.Begin("###PcoNativeChatButtons", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.AlwaysAutoResize
            | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoBackground);
        ImGui.PopStyleVar(2);
        try
        {
            if (!draw) return;
            DrawNativeButtonRow(buttonHeight, scale);
            NativeWindowMenu();
        }
        finally { ImGui.End(); }
    }
    private void DrawNativeButtonRow(float buttonHeight, float scale)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, buttonHeight * .5f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(6 * scale, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4 * scale, 0));
        try
        {
            ImGui.SetWindowFontScale(NativeFontScale());
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
                ImGui.SetWindowFontScale(NativeFontScale());
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
        if (settings.WindowTabs("main").Any()) windows.Add(new("main", "メイン###native-main", settings.Visible));
        foreach (var tab in settings.Tabs)
            if (tab.Detached) windows.Add(new(tab.Id, tab.Name + "###native-" + tab.Id, settings.Visible && tab.Visible));
        return windows;
    }
    private void ToggleNativeWindow(string id)
    {
        if (id == "main") settings.Visible = !settings.Visible;
        else
        {
            var tab = settings.Tabs.FirstOrDefault(t => t.Id == id);
            if (tab == null) return;
            if (settings.Visible && tab.Visible) tab.Visible = false;
            else { tab.Visible = true; settings.Visible = true; }
        }
        Save();
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
        return changed;
    }
    private float NativeFontScale()
    {
        var size = ImGui.GetFontSize();
        return size > 0 ? settings.NativeButtonFontSize / size : 1;
    }
    private readonly record struct NativeWindowChoice(string Id, string Label, bool Open);
}
