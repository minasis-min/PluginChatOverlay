using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace PluginChatOverlay;

public sealed partial class Plugin
{
    private bool DrawWindowDisplaySettings()
    {
        var changed = false;
        UiSection("ウィンドウの表示");
        var title = settings.ShowWindowTitle;
        if (UiCheckbox("チャットのタイトルバーを表示", ref title)) { settings.ShowWindowTitle = title; changed = true; }
        var tabs = settings.ShowTabs;
        if (UiCheckbox("チャットのタブバーを表示", ref tabs)) { SetTabBarVisible(tabs); changed = true; }
        ImGui.TextWrapped("両方OFFならログだけ表示します。タブの切替・再表示はログ欄の右クリックから行えます。");
        return changed;
    }
    private bool DrawDetailedWindowAppearance()
    {
        var changed = EnsureWindowLayoutDefaults();
        UiSection("背景の縁・内側余白・外枠");
        var alpha = settings.BackgroundAlpha * 100;
        if (UiSlider("背景アルファ (%)", ref alpha, 0, 100)) { settings.BackgroundAlpha = alpha / 100; changed = true; }
        var blur = settings.BackgroundBlur;
        if (UiCheckbox("背景の縁をぼかす", ref blur)) { settings.BackgroundBlur = blur; changed = true; }
        if (blur)
        {
            var strength = settings.BlurStrength;
            if (UiSlider("縁のぼかし幅 (px)", ref strength, .1f, 24, "%.1f")) { settings.BlurStrength = strength; changed = true; }
        }
        var customLayout = settings.CustomWindowLayout;
        if (UiCheckbox("余白・外枠をカスタマイズ", ref customLayout)) { settings.CustomWindowLayout = customLayout; changed = true; }
        ImGui.BeginDisabled(!customLayout);
        try
        {
            var x = settings.InnerPaddingX;
            if (AppearanceInteger("内側余白・横 (px)", ref x, 0, 40)) { settings.InnerPaddingX = x; changed = true; }
            var y = settings.InnerPaddingY;
            if (AppearanceInteger("内側余白・縦 (px)", ref y, 0, 40)) { settings.InnerPaddingY = y; changed = true; }
            var border = settings.BorderWidth;
            if (AppearanceInteger("外枠の太さ (px)", ref border, 0, 8)) { settings.BorderWidth = border; changed = true; }
            changed |= AppearanceColor("外枠の色", settings.BorderColor, color => settings.BorderColor = color);
            var rounding = settings.WindowRounding;
            if (AppearanceInteger("角丸 (px)", ref rounding, 0, 24)) { settings.WindowRounding = rounding; changed = true; }
            if (UiButton("余白・外枠を初期値に戻す")) { ResetWindowLayoutDefaults(); changed = true; }
        } finally { ImGui.EndDisabled(); }
        ImGui.TextWrapped("既定は従来の余白・外枠です。カスタマイズ時は外枠の太さを0にすると枠を隠せます。");
        return changed;
    }
    private bool DrawScrollbarAppearance()
    {
        var changed = false;
        UiSection("スクロールバー");
        var show = settings.ScrollbarDisplay != 0;
        if (UiCheckbox("スクロールバーを表示", ref show)) { settings.ScrollbarDisplay = show ? 1 : 0; changed = true; }
        if (show)
        {
            var automatic = settings.ScrollbarDisplay == 1;
            if (UiCheckbox("スクロール・ホバー中のみ表示", ref automatic)) { settings.ScrollbarDisplay = automatic ? 1 : 2; changed = true; }
            var left = settings.ScrollbarLeft;
            if (UiCheckbox("左側に配置", ref left)) { settings.ScrollbarLeft = left; settings.ScrollbarPositionX = null; settings.ScrollbarOffsetX = 0; changed = true; }
            changed |= DrawScrollbarColorSettings();
            changed |= DrawScrollbarPlacementSettings();
        }
        ImGui.TextWrapped("自動表示でもバーの幅を確保し、行頭位置を揃えます。非表示でもマウスホイールでスクロールできます。");
        ImGui.TextWrapped("横位置はウィンドウ左端が0です。上端・下端は0で縦余白に追従する標準の長さになります。＋で外側へ伸ばし、−で内側へ短くします。−／＋ボタンは1pxずつ調整します。ウィンドウ外には出ず、ON/OFFでは編集値を保持します。");

        return changed;
    }
    private bool DrawScrollbarColorSettings()
    {
        var changed = false;
        var rgba = ImGui.ColorConvertU32ToFloat4(settings.ResolveScrollbarColor(ImGui.GetColorU32(ImGuiCol.ScrollbarGrab)));
        var rgb = new Vector3(rgba.X, rgba.Y, rgba.Z);
        ImGui.SetNextItemWidth(240);
        if (ImGui.ColorEdit3("スクロールバーRGB", ref rgb, ImGuiColorEditFlags.Uint8))
        { settings.ScrollbarColor = ImGui.ColorConvertFloat4ToU32(new(rgb, rgba.W)); changed = true; }
        var scrollbarAlpha = rgba.W * 100;
        if (UiSlider("スクロールバーアルファ (%)", ref scrollbarAlpha, 0, 100))
        { settings.ScrollbarColor = ImGui.ColorConvertFloat4ToU32(new(rgb, scrollbarAlpha / 100)); changed = true; }
        var outline = settings.ScrollbarOutline;
        if (UiCheckbox("縁取りをつける", ref outline)) { settings.ScrollbarOutline = outline; changed = true; }
        if (outline)
        {
            changed |= AppearanceColor("スクロールバー光彩の色", settings.ScrollbarOutlineColor, color => settings.ScrollbarOutlineColor = color);
            var outlineWidth = settings.ScrollbarOutlineWidth;
            if (AppearanceInteger("スクロールバー光彩の広がり (px)", ref outlineWidth, 1, 4)) { settings.ScrollbarOutlineWidth = outlineWidth; changed = true; }
        }
        return changed;
    }

    private bool DrawScrollbarPlacementSettings()
    {
        var changed = false;
        var width = settings.ScrollbarWidth;
        if (AppearanceInteger("スクロールバーの幅 (px)", ref width, 2, 24)) { settings.ScrollbarWidth = width; changed = true; }
        var referenceWidth = PrimaryWindowTab?.Width ?? settings.Width;
        var referencePadding = settings.CustomWindowLayout ? settings.InnerPaddingX : ImGui.GetStyle().WindowPadding.X;
        var offsetX = settings.ScrollbarPositionX ?? (int)MathF.Round(Math.Clamp(settings.ScrollbarLeft ? referencePadding + settings.ScrollbarOffsetX
            : referenceWidth - referencePadding - settings.ScrollbarWidth + settings.ScrollbarOffsetX, 0, Math.Max(0, referenceWidth - settings.ScrollbarWidth)));
        if (AppearanceInteger("スクロールバー横位置・左端から (px)", ref offsetX, 0, 20000)) { settings.ScrollbarPositionX = offsetX; changed = true; }
        var top = settings.ScrollbarTopAdjustment ?? -settings.ScrollbarOffsetY;
        if (AppearanceInteger("スクロールバー上端の伸縮 (px)", ref top, -200, 200)) { settings.ScrollbarTopAdjustment = top; changed = true; }
        var bottom = settings.ScrollbarBottomAdjustment ?? settings.ScrollbarOffsetY;
        if (AppearanceInteger("スクロールバー下端の伸縮 (px)", ref bottom, -200, 200)) { settings.ScrollbarBottomAdjustment = bottom; changed = true; }
        if (UiButton("スクロールバー位置・伸縮を初期値に戻す")) { ResetScrollbarPlacement(); changed = true; }

        return changed;
    }

    private void ResetScrollbarPlacement()
    {
        settings.ScrollbarPositionX = null;
        settings.ScrollbarOffsetX = settings.ScrollbarOffsetY = 0;
        settings.ScrollbarTopAdjustment = settings.ScrollbarBottomAdjustment = 0;
    }
    private bool EnsureWindowLayoutDefaults()
    {
        if (settings.WindowLayoutInitialized) return false;
        // Preserve users who already edited the earlier custom layout controls.
        if (!settings.CustomWindowLayout && settings.InnerPaddingX == 8 && settings.InnerPaddingY == 8
            && settings.BorderWidth == 1 && settings.BorderColor == 0x806E6E80 && settings.WindowRounding == 4)
            ResetWindowLayoutDefaults();
        settings.WindowLayoutInitialized = true;
        return true;
    }
    private void ResetWindowLayoutDefaults()
    {
        var style = ImGui.GetStyle();
        settings.InnerPaddingX = (int)MathF.Round(style.WindowPadding.X);
        settings.InnerPaddingY = (int)MathF.Round(style.WindowPadding.Y);
        settings.BorderWidth = (int)MathF.Round(style.WindowBorderSize);
        settings.BorderColor = ImGui.ColorConvertFloat4ToU32(style.Colors[(int)ImGuiCol.Border]);
        settings.WindowRounding = (int)MathF.Round(style.WindowRounding);
        settings.WindowLayoutInitialized = true;
    }
    private bool DrawGlobalWindowSettings()
    {
        var changed = DrawWindowMovementSettings();
        if (UiButton("全ウィンドウの位置・サイズをリセット")) { ResetWindowGeometry(); changed = true; }
        return changed;
    }
    private bool DrawWindowMovementSettings()
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
        UiSection("スナップ");
        var snap = settings.SnapWindows;
        if (UiCheckbox("画面端・他のチャット窓へスナップ", ref snap)) { settings.SnapWindows = snap; changed = true; }
        var distance = settings.SnapDistance;
        if (UiSlider("スナップ距離 (px)", ref distance, 4, 40)) { settings.SnapDistance = distance; changed = true; }
        return changed;
    }
    private bool DrawTextDecorationSettings()
    {
        UiSection("文字間・縁取り");
        var changed = false;
        var spacing = settings.LetterSpacing;
        if (AppearanceInteger("横方向の文字間 (px)", ref spacing, -2, 12))
        { settings.LetterSpacing = spacing; RebuildChatFont(); changed = true; }
        ImGui.TextWrapped("文字間はPCO専用のフォントに適用します。マイナス値では文字が重なる場合があります。");
        var edge = settings.TextOutline;
        if (UiCheckbox("文字に縁取りを付ける", ref edge)) { settings.TextOutline = edge; changed = true; }
        if (edge)
        {
            var strength = settings.OutlineStrength;
            if (UiSlider("縁取りの強さ", ref strength, .1f, 3, "%.1f")) { settings.OutlineStrength = strength; changed = true; }
            changed |= AppearanceColor("縁取りの色", settings.OutlineColor, color => settings.OutlineColor = color);
        }
        return changed;
    }
    private bool AppearanceInteger(string label, ref int value, int minimum, int maximum)
    {
        ImGui.SetNextItemWidth(200);
        var changed = UiInputInt(label, ref value, 1, 4);
        if (changed) value = Math.Clamp(value, minimum, maximum);
        return changed;
    }
    private static bool AppearanceColor(string label, uint value, Action<uint> save)
    {
        ImGui.SetNextItemWidth(240);
        var rgba = ImGui.ColorConvertU32ToFloat4(value);
        if (!ImGui.ColorEdit4(label, ref rgba)) return false;
        save(ImGui.ColorConvertFloat4ToU32(rgba));
        return true;
    }
}
