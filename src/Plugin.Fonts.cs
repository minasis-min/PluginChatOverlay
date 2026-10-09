using Dalamud.Interface.FontIdentifier;
using Dalamud.Interface.ImGuiFontChooserDialog;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Bindings.ImGui;

namespace PluginChatOverlay;

public sealed partial class Plugin
{
    private IFontAtlas? fontAtlas;
    private IFontHandle? chatFont;
    private SingleFontChooserDialog? fontChooser;
    private string fontNotice = "";
    private string fontName = "Dalamud標準";
    private bool fontWasAvailable;
    private void ApplyUiVisibility()
    {
        pi.UiBuilder.DisableCutsceneUiHide = !settings.HideDuringCutscene;
        // User UI hide and gpose continue to follow Dalamud's normal behavior.
    }
    private void RebuildChatFont()
    {
        settings.NormalizeLineSpacing();
        chatFont?.Dispose(); chatFont = null; fontWasAvailable = false; fontNotice = ""; fontName = "Dalamud標準";
        fontSizeEdited = false;
        InvalidateRowLayouts();
        try
        {
            var id = FontChoice.Restore(settings.FontIdType, settings.FontIdJson) ?? DalamudDefaultFontAndFamilyId.Instance;
            if (settings.FontIdType.Length > 0) fontName = id.Family.GetLocalizedName("ja-jp") + " / " + id.GetLocalizedName("ja-jp");
            fontAtlas ??= pi.UiBuilder.CreateFontAtlas(FontAtlasAutoRebuildMode.Async, false, "Plugin Chat Overlay fonts");
            chatFont = new SingleFontSpec { FontId = id, SizePx = settings.FontSize }.CreateFontHandle(fontAtlas);
        }
        catch (Exception ex) { fontNotice = "フォントを復元できないためDalamud標準フォントを使用します。"; log.Warning(ex, "Chat font unavailable"); }
    }
    private void UpdateFontChooser()
    {
        if (fontChooser != null)
        {
            fontChooser.Draw();
            if (fontChooser.ResultTask.IsCompleted)
            {
                try
                {
                    if (fontChooser.ResultTask.IsCompletedSuccessfully && fontChooser.ResultTask.Result is { } selected)
                    {
                        FontChoice.Store(settings, selected.FontId);
                        settings.FontSize = Math.Clamp(MathF.Round(selected.SizePx), 10, 36);
                        RebuildChatFont(); Save();
                    }
                }
                catch (Exception ex) { fontNotice = "フォントを変更できませんでした。"; log.Warning(ex, "Font selection failed"); }
                finally { fontChooser.Dispose(); fontChooser = null; }
            }
        }
        if (chatFont != null && chatFont.Available != fontWasAvailable)
        {
            fontWasAvailable = chatFont.Available;
            InvalidateRowLayouts();
        }
        if (chatFont?.LoadException != null) fontNotice = "フォント読込失敗。Dalamud標準フォントで表示します。";
    }
    private bool DrawFontSettings()
    {
        ImGui.TextWrapped("フォント: " + fontName);
        if (UiButton("フォントを選択"))
        {
            fontChooser?.Dispose();
            fontChooser = new((Dalamud.Interface.UiBuilder)pi.UiBuilder, false, "Plugin Chat Overlay font chooser")
            { Title = "チャットのフォント", PreviewText = "[23:34] 日本語チャット ABC abc 0123" };
            IFontId id = DalamudDefaultFontAndFamilyId.Instance;
            try { id = FontChoice.Restore(settings.FontIdType, settings.FontIdJson) ?? id; }
            catch { /* A missing saved font must not prevent opening the chooser. */ }
            fontChooser.SelectedFont = new SingleFontSpec { FontId = id, SizePx = settings.FontSize };
        }
        ImGui.SameLine();
        var changed = UiButton("フォントを標準に戻す");
        if (changed) { settings.FontIdType = ""; settings.FontIdJson = ""; RebuildChatFont(); }
        ImGui.TextWrapped("Dalamudのフォント選択画面で、ゲーム内・システムのフォントを選べます。日本語はプレビューで確認してください。");
        if (fontNotice.Length > 0) ImGui.TextWrapped(fontNotice);
        return changed;
    }
}
