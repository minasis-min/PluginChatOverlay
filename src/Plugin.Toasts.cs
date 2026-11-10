using Dalamud.Interface.FontIdentifier;
using Dalamud.Interface.ImGuiFontChooserDialog;
using Dalamud.Interface.ManagedFontAtlas;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiSeStringRenderer;
using Dalamud.Interface.Utility;
using Dalamud.Game.Gui.Toast;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Textures;
namespace PluginChatOverlay;
public sealed partial class Plugin
{
    private readonly ToastQueue toastQueue = new(() => Environment.TickCount64);
    private QuestToastCapture? toastCapture;
    private IToastGui? toastService;
    private ITextureProvider? toastTextures;
    private long toastReadyAt;
    private IFontHandle? toastFont;
    private void RebuildToastFont()
    {
        StopPluginToasts();
        toastFont?.Dispose(); toastFont = null;
        try
        {
            fontAtlas ??= pi.UiBuilder.CreateFontAtlas(FontAtlasAutoRebuildMode.Async, false, "Plugin Chat Overlay fonts");
            toastFont = new SingleFontSpec { FontId = DalamudDefaultFontAndFamilyId.Instance, SizePx = settings.ToastFontSize }.CreateFontHandle(fontAtlas);
        }
        catch (Exception ex) { log.Warning(ex, "Toast font unavailable; native notifications retained"); }
    }

    private bool ToastAllowed => !disposed && !uiFault && toastFont?.Available == true && settings.SeparatePluginToasts && settings.Visible && clientState.IsLoggedIn
        && !chatHiddenByNativeExpansion && (!settings.HideDuringCutscene || !pi.UiBuilder.CutsceneActive);
    private void InitializePluginToasts(ITextureProvider textures, IToastGui toast)
    {
        toastTextures = textures; toastService = toast; RebuildToastFont();
        toastCapture = new(toast, () => framework.IsInFrameworkUpdateThread,
            () => ToastAllowed && toastReadyAt != 0 && Environment.TickCount64 - toastReadyAt < 250,
            (bytes, position, icon) => toastQueue.TryAdd(bytes, position, icon));
    }
    private void DrawPluginToasts()
    {
        if (!ToastAllowed) { StopPluginToasts(); return; }
        var viewport = ImGui.GetMainViewport();
        if (viewport.Size.X <= 0 || viewport.Size.Y <= 0) { StopPluginToasts(); return; }
        using var toastFontScope = toastFont!.Push();
        var banners = toastQueue.Snapshot();
        try
        {
        float? stackY = null;
        var toastIndex = 0;
        foreach (var toast in banners)
        {
            var scale = ImGuiHelpers.GlobalScale;
            var iconSize = toast.IconId == 0 ? 0 : 42 * scale;
            var maxWidth = Math.Max(1, Math.Min(viewport.Size.X - 32, 800 * scale));
            var textStyle = new SeStringDrawParams { WrapWidth = Math.Max(1, maxWidth - 2 * iconSize - 64 * scale), Color = 0xFFFFFFFF,
                Font = ImGui.GetFont(), FontSize = ImGui.GetFontSize(), Edge = true, EdgeColor = 0xFFDF8500 };
            var measure = textStyle; measure.TargetDrawList = default(ImDrawListPtr); measure.ScreenOffset = Vector2.Zero;
            var measured = ImGuiHelpers.SeStringWrapped(toast.Bytes, measure).Size;
            var width = Math.Clamp(measured.X + 2 * iconSize + 64 * scale, Math.Min(200 * scale, maxWidth), maxWidth);
            var anchor = viewport.Pos + viewport.Size * new Vector2(.5f, .42f);
            ImGui.SetNextWindowViewport(viewport.ID);
            var height = Math.Max(measured.Y, iconSize) + 6 * scale;
            stackY ??= anchor.Y - height / 2;
            ImGui.SetNextWindowPos(new(anchor.X, stackY.Value), ImGuiCond.Always, new(.5f, 0));
            stackY += height;
            ImGui.SetNextWindowSize(new(width, height));
            ImGui.SetNextWindowBgAlpha(0);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(16, 3) * scale);
            var visible = ImGui.Begin("###PCO.PluginToast." + toastIndex++,
                ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav);
            try
            {
                if (!visible) continue;
                var first = ImGui.GetWindowPos(); var last = first + ImGui.GetWindowSize();
                var opacity = Math.Clamp(Math.Min(Environment.TickCount64 - toast.Began, toast.Expires - Environment.TickCount64) / 200f, 0, 1);
                SoftWindowBackground.Draw(ImGui.GetWindowDrawList(), first + new Vector2(16, 4) * scale, last - new Vector2(16, 4) * scale, settings.ToastBackgroundColor, (settings.ToastBackgroundColor >> 24) / 255f * opacity, 5 * scale, 16 * scale);
                // Center the measured text itself on the background. Reserve the
                // same icon clearance on both sides so the icon cannot shift it.
                var textPosition = first + (ImGui.GetWindowSize() - measured) / 2;
                if (toast.IconId != 0 && toastTextures?.GetFromGameIcon(new GameIconLookup(toast.IconId)).GetWrapOrDefault() is { } icon)
                {
                    ImGui.SetCursorScreenPos(new(textPosition.X - iconSize - 8 * scale, first.Y + (height - iconSize) / 2));
                    ImGui.Image(icon.Handle, new Vector2(iconSize), Vector2.Zero, Vector2.One, new Vector4(1, 1, 1, opacity));
                }
                ImGui.SetCursorScreenPos(textPosition);
                textStyle.Opacity = opacity;
                ImGuiHelpers.SeStringWrapped(toast.Bytes, textStyle);
            }
            finally { ImGui.End(); ImGui.PopStyleVar(2); }
        }
        }
        catch (Exception ex)
        {
            // Stop interception before restoring captured notifications to the host queue.
            // A renderer failure must not cause a recapture loop or suspend chat separation.
            try { toastCapture?.Suspend("描画に失敗しました。再ロードしてください。"); }
            finally { StopPluginToasts(); }
            foreach (var banner in banners)
                try { toastService?.ShowQuest(Dalamud.Game.Text.SeStringHandling.SeString.Parse(banner.Bytes), new QuestToastOptions { Position = banner.Position, IconId = banner.IconId, PlaySound = false }); }
                catch (Exception fallback) { log.Warning(fallback, "Unable to restore captured toast"); }
            log.Warning(ex, "Plugin toast rendering stopped; original notifications are retained");
            return;
        }
        toastReadyAt = Environment.TickCount64;
    }
    private void StopPluginToasts() { toastReadyAt = 0; toastQueue.Clear(); }
    private bool DrawPluginToastToggle()
    {
        UiSection("トースト通知");
        var enabled = settings.SeparatePluginToasts;
        var changed = UiCheckbox("プラグイン由来のトーストをPCOの描画で表示", ref enabled);
        if (changed) { settings.SeparatePluginToasts = enabled; StopPluginToasts(); }
        UiDescription("対応するプラグイン由来のトースト通知をPCOが表示します。未対応の通知、ネイティブ由来の通知は通常通り表示されます。");
        if (UiButton("通知テスト")) TestPluginToast();
        return changed;
    }
    private bool DrawPluginToastSettings() => SettingsCard("DrawPluginToastSettings", DrawPluginToastSettingsBody);
    private bool DrawPluginToastSettingsBody()
    {
        UiSection("トースト通知");
        var changed = false;
        var fontSize = settings.ToastFontSize;
        if (UiInputInt("文字サイズ (px)", ref fontSize))
        {
            settings.ToastFontSize = Math.Clamp(fontSize, 16, 48);
            RebuildToastFont(); changed = true;
        }
        var background = ImGui.ColorConvertU32ToFloat4(settings.ToastBackgroundColor);
        var colorChanged = minasisFrame
            ? ui.ColorInput("toast-background", "背景色・透明度", ref background)
            : ImGui.ColorEdit4("背景色・透明度", ref background);
        if (colorChanged) { settings.ToastBackgroundColor = ImGui.ColorConvertFloat4ToU32(background); changed = true; }
        if (UiButton("背景色を初期値に戻す")) { settings.ToastBackgroundColor = Settings.DefaultToastBackgroundColor; changed = true; }
        if (UiButton("トーストの表示テスト")) TestPluginToast();
        return changed;
    }
    private void TestPluginToast(bool pair = false)
    {
        if (pair)
        {
            toastService?.ShowQuest("PCO 通知A", new QuestToastOptions { IconId = 111, PlaySound = false });
            toastService?.ShowQuest("PCO 通知B", new QuestToastOptions { IconId = 111, PlaySound = false });
        }
        else toastService?.ShowQuest("PCO トースト表示テスト", new QuestToastOptions { IconId = 111, PlaySound = false });
    }
}
