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
    private static readonly string CompatibilityVersions = "Dalamud " + typeof(Dalamud.Plugin.Services.IChatGui).Assembly.GetName().Version
        + " / FFXIVClientStructs " + typeof(FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase).Assembly.GetName().Version;
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
        var copyGame = settings.CopyGameLogs;
        if (UiCheckbox("ゲームログをコピー", ref copyGame)) { settings.CopyGameLogs = copyGame; changed = true; }
        ImGui.TextWrapped("ゲーム側のログを残したままPCOにも表示します。表示項目は「タブ」で選択します。");
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
        ImGui.TextWrapped("UI非表示・グループポーズ中はDalamudの設定に従います。非表示中もログを取り込みます。");
        changed |= DrawWindowDisplaySettings();
        return changed;
    }
    private void DrawDiagnostics()
    {
        UiSection("API互換性（Debug）");
        ImGui.TextUnformatted(CompatibilityVersions);
        ImGui.TextWrapped("通知分離: " + capture.CompatibilityDiagnostic);
        ImGui.TextWrapped("ゲームログコピー: " + GameLogCapture.Diagnostic(settings.CopyGameLogs,
            gameLogCatalog.Available, gameLogCatalog.Diagnostic, capture.Compatible, capture.CompatibilityDiagnostic,
            uiFault, gameCapture.Subscribed, gameCopySelection.HasSelection));
        ImGui.TextWrapped("ネイティブツールチップ: " + NativeTooltipCompatibility.Diagnostic);
        if (links.TooltipStatus.Length > 0) ImGui.TextWrapped(links.TooltipStatus);
        ImGui.TextWrapped("チャット拡大中の非表示: " + nativeChatVisibility.Diagnostic);
        ImGui.TextWrapped("文章選択: " + fragmentLayout.CompatibilityDiagnostic);
        UiSection("動作状況");
        ImGui.TextWrapped(capture.Status);
        ImGui.TextWrapped("他プラグインが先に非表示にしたログは捕捉しません。");
        if (uiFault)
        {
            ImGui.TextWrapped("描画エラー: 分離停止中です。再ロードしてください。残った履歴はコピーで回収できます。");
            if (UiButton("履歴をコピーして回収")) ImGui.SetClipboardText(string.Join(Environment.NewLine, history.Snapshot().Select(Plain)));
        }
        ImGui.TextWrapped("初期状態は通知分離・ゲームログコピーともにOFFです。");
        ImGui.TextUnformatted($"ゲームログのコピー {gameCapture.Copied} / 失敗 {gameCapture.Failures}");
        ImGui.TextUnformatted($"キュー経路確認 {capture.QueueMatches} / 捕捉 {capture.Captured} / 非対象・容量超過 {capture.Passed} / 失敗 {capture.Failures}");
        ImGui.TextUnformatted($"履歴 {history.Snapshot().Length} / 削除 {history.Evictions} / テストリンク発火 {testLinkClicks}");
        ImGui.TextUnformatted($"入手ログからの追記分離 {capture.AppendedLoot}件（元の入手ログはバニラに残します）");
        ImGui.TextWrapped("操作対応: Dalamud登録リンク・マップ座標。アイテム・プレイヤー・URLの操作は未対応。ゲームログはコピー設定とタブのフィルターに従います。");
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
        ImGui.TextWrapped("10～36px。本文と時刻に適用します。");
        var lineSpacing = settings.LineSpacing;
        ImGui.SetNextItemWidth(180);
        if (UiInputInt("行の間隔 (調整px)", ref lineSpacing, 1, 4))
        { settings.LineSpacing = lineSpacing; settings.NormalizeLineSpacing(); changed = true; }
        ImGui.SameLine();
        if (UiButton("行の間隔を初期値に戻す")) { settings.LineSpacing = 0; changed = true; }
        ImGui.TextWrapped($"{settings.MinimumLineSpacing}～24px。マイナスで狭く、プラスで広くなります。0が標準です。詰めすぎると文字が重なります。");
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
        if (UiCheckbox("アイテムにマウスを重ねると詳細を表示", ref tooltips)) { settings.LinkTooltips = tooltips; changed = true; }
        ImGui.TextWrapped("アイテムリンクにゲーム標準の詳細を表示します。HQ品・イベントアイテムにも対応。");
        if (links.TooltipStatus.Length > 0) ImGui.TextWrapped(links.TooltipStatus);
        changed |= DrawTimestampSettings();
        ImGui.TextWrapped("本文の色はゲーム・出力プラグインの指定に従います。");
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
        if (UiButton("初期値に戻す（白）")) { settings.TimestampColor = 0xFFFFFFFF; changed = true; }
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
        if (UiCheckbox("ドラッグ選択後に自動コピー", ref autoCopy)) { settings.CopyOnDragRelease = autoCopy; changed = true; }
        ImGui.TextWrapped("Ctrl+Cでコピーすると、選択とフォーカスを解除します。OFFでも右クリック・自動コピーは利用できます。");
        if (fragmentLayout.Status.Length > 0) ImGui.TextWrapped(fragmentLayout.Status + "。右クリックの1件コピーは利用できます。");
        return changed;
    }
    private bool DrawHistorySettings()
    {
        UiSection("履歴の保持・コピー");
        var changed = false; var limit = settings.HistoryLimit;
        if (UiInputInt("プラグイン通知の履歴件数 (10–1000)", ref limit)) { settings.HistoryLimit = Math.Clamp(limit, 10, History.MaxRecords); history.ApplyLimit(settings.HistoryLimit); PruneHistoryCaches(); changed = true; }
        var gameLimit = settings.GameHistoryLimit;
        if (UiInputInt("ゲームログの履歴件数 (10–1000)", ref gameLimit))
        { settings.GameHistoryLimit = Math.Clamp(gameLimit, 10, History.MaxRecords); history.ApplyGameLimit(settings.GameHistoryLimit); PruneHistoryCaches(); changed = true; }
        if (UiButton("履歴全体をコピー")) ImGui.SetClipboardText(string.Join(Environment.NewLine, history.Snapshot().Select(Plain)));
        if (UiButton("履歴をクリア")) ClearHistory();
        return changed;
    }

}
