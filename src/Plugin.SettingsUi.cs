using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace PluginChatOverlay;

public sealed partial class Plugin
{
    private string selectedSettingsTab = "";
    private bool selectChatTabSettings, focusConfig;
    private void OpenTabSettings(string tabId)
    {
        selectedSettingsTab = tabId;
        configOpen = true; selectChatTabSettings = true; focusConfig = true;
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
        { state.ResetGeometry = true; state.HeaderDragging = false; state.RequestedSize = null; }
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
        var title = settings.ShowWindowTitle;
        if (UiCheckbox("チャットのタイトルバーを表示", ref title)) { settings.ShowWindowTitle = title; changed = true; }
        var tabs = settings.ShowTabs;
        if (UiCheckbox("チャットのタブバーを表示", ref tabs)) { SetTabBarVisible(tabs); changed = true; }
        ImGui.TextWrapped("タイトルとタブは個別に隠せます。両方OFFならログだけ表示します。非表示中のタブ切替・再表示はログ欄の右クリックから行えます。タイトルバーの標準メニュー・最小化は表示中のみ利用できます。");
        var cutscene = settings.HideDuringCutscene;
        if (UiCheckbox("カットシーン中は非表示", ref cutscene)) { settings.HideDuringCutscene = cutscene; changed = true; }
        var logout = settings.HideLoggedOut;
        if (UiCheckbox("ログアウト時非表示", ref logout)) { settings.HideLoggedOut = logout; changed = true; }
        ImGui.TextWrapped("UIを隠した時・グループポーズ中の表示はDalamudの標準設定に従います。非表示中もログは履歴へ保存されます。");
        if (UiButton("位置・サイズをリセット"))
        {
            ResetWindowGeometry();
            changed = true;
        }
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
    private bool DrawAppearance()
    {
        UiSection("背景・文字");
        var changed = false;
        var alpha = settings.BackgroundAlpha * 100;
        if (UiSlider("背景アルファ (%)", ref alpha, 0, 100)) { settings.BackgroundAlpha = alpha / 100; changed = true; }
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
        if (UiInputInt("行間 (調整px)", ref lineSpacing, 1, 4))
        { settings.LineSpacing = lineSpacing; settings.NormalizeLineSpacing(); changed = true; }
        ImGui.SameLine();
        if (UiButton("行間を標準に戻す")) { settings.LineSpacing = 0; changed = true; }
        ImGui.TextWrapped($"{settings.MinimumLineSpacing}～24px。マイナスで行間を詰め、プラスで広げます。チャット同士と折り返した行に適用します。−／＋ボタンは1pxずつ調整します。0で従来の間隔です。下限は文字サイズの半分まで。詰めすぎると文字が重なる場合があります。");
        changed |= DrawFontSettings();
        UiSection("リンク・時刻");
        var tooltips = settings.LinkTooltips;
        if (UiCheckbox("ネイティブリンクにホバーでツールチップを表示", ref tooltips)) { settings.LinkTooltips = tooltips; changed = true; }
        ImGui.TextWrapped("アイテムリンクにはゲーム標準のItemDetailを表示します。HQ品・イベントアイテムにも対応します。マップ・プレイヤー・クエストには独自のツールチップを表示しません。");
        if (links.TooltipStatus.Length > 0) ImGui.TextWrapped(links.TooltipStatus);
        var time = settings.ShowTime;
        if (UiCheckbox("時刻表示", ref time)) { settings.ShowTime = time; changed = true; }
        var rgba = ImGui.ColorConvertU32ToFloat4(settings.TimestampColor); var rgb = new Vector3(rgba.X, rgba.Y, rgba.Z);
        if (ImGui.ColorEdit3("時刻の色", ref rgb)) { settings.TimestampColor = ImGui.ColorConvertFloat4ToU32(new(rgb, 1)); changed = true; }
        ImGui.SameLine();
        if (UiButton("Default（白）")) { settings.TimestampColor = 0xFFFFFFFF; changed = true; }
        ImGui.TextWrapped("本文はゲームのチャット種別の色と、出力プラグインが指定した部分色をそのまま使用します。");
        if (palette.MissingKinds.Count > 0) ImGui.TextWrapped("標準色を取得できなかった種別: " + string.Join(", ", palette.MissingKinds));
        return changed;
    }
    private bool DrawInteraction()
    {
        UiSection("ウィンドウ操作");
        var changed = false;
        var move = settings.AllowMove;
        if (UiCheckbox("チャットの移動を許可", ref move)) { settings.AllowMove = move; changed = true; }
        var resize = settings.AllowResize;
        if (UiCheckbox("チャットのサイズ変更を許可", ref resize)) { settings.AllowResize = resize; changed = true; }
        ImGui.TextWrapped("移動・サイズ変更・タイトルバーの設定はメイン窓と独立窓の両方に適用します。固定・クリック透過中は許可設定より固定が優先されます。");
        var primary = PrimaryWindowTab;
        var pinned = primary?.Pinned ?? settings.Pinned;
        if (UiCheckbox("最初の窓を固定（移動・サイズ変更を無効）", ref pinned))
        { if (primary == null) settings.Pinned = pinned; else primary.Pinned = pinned; changed = true; }
        var pass = primary?.ClickThrough ?? settings.ClickThrough;
        if (UiCheckbox("最初の窓をクリック透過", ref pass))
        { if (primary == null) settings.ClickThrough = pass; else primary.ClickThrough = pass; changed = true; }
        if (UiButton("最初の窓の最小化・固定・透過を解除"))
        {
            if (primary == null) { settings.Pinned = false; settings.ClickThrough = false; }
            else { primary.Pinned = false; primary.ClickThrough = false; primary.Visible = true; }
            settings.Visible = true;
            if (windowModels.TryGetValue(primary?.Id ?? "main", out var host)) { host.Collapsed = false; host.CollapsedCondition = ImGuiCond.Always; host.RequestFocus = true; }
            changed = true;
        }
        ImGui.TextWrapped("移動はタイトル（プラグイン名）とタブのドラッグだけです。ログ欄の空白はドラッグしても窓を移動しません。");
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
        UiSection("スナップ");
        var snap = settings.SnapWindows;
        if (UiCheckbox("画面端・他のチャット窓へスナップ", ref snap)) { settings.SnapWindows = snap; changed = true; }
        var distance = settings.SnapDistance;
        if (UiSlider("スナップ距離 (px)", ref distance, 4, 40)) { settings.SnapDistance = distance; changed = true; }
        if (fragmentLayout.Status.Length > 0) ImGui.TextWrapped(fragmentLayout.Status + "。右クリックの1件コピーは利用できます。");
        return changed;
    }
    private bool DrawHistorySettings()
    {
        UiSection("履歴の保持・コピー");
        var changed = false; var limit = settings.HistoryLimit;
        if (UiInputInt("履歴件数 (10–1000)", ref limit)) { settings.HistoryLimit = Math.Clamp(limit, 10, History.MaxRecords); history.ApplyLimit(settings.HistoryLimit); PruneHistoryCaches(); changed = true; }
        if (UiButton("履歴全体をコピー")) ImGui.SetClipboardText(string.Join(Environment.NewLine, history.Snapshot().Select(Plain)));
        if (UiButton("履歴をクリア")) { history.Clear(); PruneHistoryCaches(); foreach (var view in views.Values) { view.Selection.Clear(); view.Unread = 0; view.Follow = true; } }
        return changed;
    }

}
