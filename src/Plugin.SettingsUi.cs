using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace PluginChatOverlay;

public sealed partial class Plugin
{
    private string selectedSettingsTab = "";
    private bool selectChatTabSettings, focusConfig, scrollChatTabSettings;
    private void OpenTabSettings(string tabId)
    {
        selectedSettingsTab = tabId;
        configOpen = true; selectChatTabSettings = true; focusConfig = true; scrollChatTabSettings = true;
    }
    private string sizeEditingTab = "";
    private int editedWindowWidth, editedWindowHeight;
    private bool windowSizeEdited;
    private int editedFontSize;
    private bool fontSizeEdited;
    private void ResetWindowGeometry()
    {
        settings.ResetGeometry(); resetGeometry = true;
        foreach (var state in views.Values)
        { state.ResetGeometry = true; state.HeaderDragging = false; state.PendingAltFrame = -1; state.RequestedSize = null; }
    }
    private bool DrawBasicSettings()
    {
        var changed = false;
        if (!minasisFrame) changed |= DrawUiStyleSettings();
        else UiSection("チャットの表示");
        var enabled = settings.Enabled;
        if (UiCheckbox("プラグイン通知分離", ref enabled)) { settings.Enabled = enabled; changed = true; }
        var visible = settings.Visible;
        if (UiCheckbox("チャット窓を表示", ref visible)) { settings.Visible = visible; changed = true; }
        var cutscene = settings.HideDuringCutscene;
        if (UiCheckbox("カットシーン中は非表示", ref cutscene)) { settings.HideDuringCutscene = cutscene; changed = true; }
        var logout = settings.HideLoggedOut;
        if (UiCheckbox("ログアウト時非表示", ref logout)) { settings.HideLoggedOut = logout; changed = true; }
        var expandedChat = settings.HideWhenNativeChatExpanded;
        if (UiCheckbox("ゲームのチャットログ拡大中は非表示", ref expandedChat)) { settings.HideWhenNativeChatExpanded = expandedChat; changed = true; }
        if (nativeChatVisibility.Compatible == false)
            ImGui.TextWrapped("現在のゲームバージョンでは、チャット拡大中の自動非表示を利用できません。");
        ImGui.TextWrapped("UIを隠した時・グループポーズ中の表示はDalamudの標準設定に従います。非表示中もログは履歴へ保存されます。");
        changed |= DrawWindowDisplaySettings();
        return changed;
    }
    private void DrawDiagnostics()
    {
        ImGui.TextWrapped(capture.Status);
        ImGui.TextWrapped("他プラグインが先に非表示にしたログは捕捉しません。");
        if (uiFault)
        {
            ImGui.TextWrapped("描画エラー: 分離停止中です。再ロードしてください。残った履歴はコピーで回収できます。");
            if (UiButton("履歴をコピーして回収")) ImGui.SetClipboardText(string.Join(Environment.NewLine, history.Snapshot().Select(Plain)));
        }
        ImGui.TextWrapped("初期状態は分離OFFです。");
        ImGui.TextUnformatted($"キュー経路確認 {capture.QueueMatches} / 捕捉 {capture.Captured} / 非対象・容量超過 {capture.Passed} / 失敗 {capture.Failures}");
        ImGui.TextUnformatted($"履歴 {history.Snapshot().Length} / 削除 {history.Evictions} / テストリンク発火 {testLinkClicks}");
        ImGui.TextUnformatted($"入手ログからの追記分離 {capture.AppendedLoot}件（元の入手ログはバニラに残します）");
        ImGui.TextWrapped("操作対応: Dalamud登録リンク・マップ座標。アイテム・プレイヤー・URLの操作は未対応。直接ゲーム出力は捕捉対象外。");
    }
    private bool DrawChatTextSettings()
    {
        UiSection("フォント・文章の間隔");
        var changed = false;
        if (!fontSizeEdited) editedFontSize = (int)MathF.Round(settings.FontSize);
        ImGui.SetNextItemWidth(180);
        if (UiInputInt("文字サイズ (px)", ref editedFontSize, 1, 5)) fontSizeEdited = true;
        if (UiButton("文字サイズを適用"))
        {
            settings.FontSize = Math.Clamp(editedFontSize, 10, 36);
            RebuildChatFont(); changed = true;
        }
        ImGui.TextWrapped("10～36px。指定サイズでフォントを生成し、本文と時刻に適用します。");
        var lineSpacing = settings.LineSpacing;
        ImGui.SetNextItemWidth(180);
        if (UiInputInt("行の間隔 (調整px)", ref lineSpacing, 1, 4))
        { settings.LineSpacing = lineSpacing; settings.NormalizeLineSpacing(); changed = true; }
        ImGui.SameLine();
        if (UiButton("行の間隔を初期値に戻す")) { settings.LineSpacing = 0; changed = true; }
        ImGui.TextWrapped($"{settings.MinimumLineSpacing}～24px。マイナスで行間を詰め、プラスで広げます。文字の行高を調整します。ログ同士の余白は別に調整できます。−／＋ボタンは1pxずつ調整します。0で従来の間隔です。下限は文字サイズの半分まで。詰めすぎると文字が重なる場合があります。");
        ImGui.BeginDisabled(!settings.IsCustomAppearance);
        try
        {
            var scale = ChatStyleSnapshot.Scale(Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale);
            var gap = (settings.CustomMessageGap ?? 0) * scale;
            ImGui.SetNextItemWidth(180);
            var gapChanged = minasisFrame
                ? ui.InputFloat("message-gap", "ログ同士の間隔 (px)", ref gap, 1, true, 0, 80 * scale, "%.1f")
                : ImGui.InputFloat("ログ同士の間隔 (px)", ref gap, 1, 4, "%.1f");
            if (gapChanged && float.IsFinite(gap)) { settings.CustomMessageGap = Math.Clamp(gap / scale, 0, 80); changed = true; }
            ImGui.SameLine();
            if (UiButton("ログの間隔を初期値に戻す"))
            { settings.CustomMessageGap = settings.CustomChatStyle?.MessageGap ?? ImGui.GetStyle().ItemSpacing.Y / scale; changed = true; }
        }
        finally { ImGui.EndDisabled(); }
        ImGui.TextWrapped("ログ同士の余白です。カスタム時に適用します。");
        changed |= DrawFontSettings();
        DrawCustomAppearanceNotice();
        ImGui.BeginDisabled(!settings.IsCustomAppearance);
        try { changed |= DrawTextDecorationSettings(); }
        finally { ImGui.EndDisabled(); }
        return changed;
    }
    private static readonly MinasisUI.UiChoice[] TimestampFormats = [new("padded", "[01:23]"), new("short", "[1:23]")];
    private bool DrawChatLinkSettings()
    {
        var changed = false;
        UiSection("リンク・時刻");
        var tooltips = settings.LinkTooltips;
        if (UiCheckbox("ネイティブリンクにホバーでツールチップを表示", ref tooltips)) { settings.LinkTooltips = tooltips; changed = true; }
        ImGui.TextWrapped("アイテムリンクにはゲーム標準のItemDetailを表示します。HQ品・イベントアイテムにも対応します。マップ・プレイヤー・クエストには独自のツールチップを表示しません。");
        if (links.TooltipStatus.Length > 0) ImGui.TextWrapped(links.TooltipStatus);
        changed |= DrawTimestampSettings();
        ImGui.TextWrapped("本文はゲームのチャット種別の色と、出力プラグインが指定した部分色をそのまま使用します。");
        if (palette.MissingKinds.Count > 0) ImGui.TextWrapped("標準色を取得できなかった種別: " + string.Join(", ", palette.MissingKinds));
        return changed;
    }
    private bool DrawTimestampSettings()
    {
        var changed = false;
        var time = settings.ShowTime;
        if (UiCheckbox("時刻表示", ref time)) { settings.ShowTime = time; changed = true; }
        var format = settings.PadTimestampHour ? 0 : 1;
        DrawCustomAppearanceNotice();
        ImGui.BeginDisabled(!settings.IsCustomAppearance);
        try
        {
            ImGui.SetNextItemWidth(160);
            var formatChanged = minasisFrame
                ? ui.Dropdown("timestamp-format", "時刻の形式", ref format, TimestampFormats)
                : ImGui.Combo("時刻の形式", ref format, "[01:23]\0[1:23]\0");
            if (formatChanged) { settings.PadTimestampHour = format == 0; changed = true; }
        }
        finally { ImGui.EndDisabled(); }
        var rgba = ImGui.ColorConvertU32ToFloat4(settings.TimestampColor); var rgb = new Vector3(rgba.X, rgba.Y, rgba.Z);
        ImGui.SetNextItemWidth(240);
        if (ImGui.ColorEdit3("時刻の色", ref rgb)) { settings.TimestampColor = ImGui.ColorConvertFloat4ToU32(new(rgb, 1)); changed = true; }
        ImGui.SameLine();
        if (UiButton("Default（白）")) { settings.TimestampColor = 0xFFFFFFFF; changed = true; }
        return changed;
    }

    private bool DrawInteraction()
    {
        var changed = DrawGlobalWindowSettings();
        UiSection("選択・コピー");
        var ctrlCopy = settings.CopyWithCtrlC;
        if (UiCheckbox("Ctrl+Cで選択した文章をコピー", ref ctrlCopy))
        {
            settings.CopyWithCtrlC = ctrlCopy;
            if (!ctrlCopy) copyInput.Reset();
            changed = true;
        }
        var autoCopy = settings.CopyOnDragRelease;
        if (UiCheckbox("ドラッグで選択して離した時に自動コピー", ref autoCopy)) { settings.CopyOnDragRelease = autoCopy; changed = true; }
        ImGui.TextWrapped("Ctrl+Cコピーは既定ONです。有効時は選択をコピーして解除し、窓を非アクティブにします。OFF時はゲーム・他プラグインのCtrl+Cを抑止しません。右クリックコピーと任意の自動コピーは引き続き利用できます。");
        if (fragmentLayout.Status.Length > 0) ImGui.TextWrapped(fragmentLayout.Status + "。右クリックの1件コピーは利用できます。");
        return changed;
    }
    private bool DrawHistorySettings()
    {
        UiSection("履歴の保持・コピー");
        var changed = false; var limit = settings.HistoryLimit;
        if (UiInputInt("履歴件数 (10–1000)", ref limit)) { settings.HistoryLimit = Math.Clamp(limit, 10, History.MaxRecords); history.ApplyLimit(settings.HistoryLimit); PruneHistoryCaches(); changed = true; }
        if (UiButton("履歴全体をコピー")) ImGui.SetClipboardText(string.Join(Environment.NewLine, history.Snapshot().Select(Plain)));
        if (UiButton("履歴をクリア")) ClearHistory();
        return changed;
    }

}
