using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;

namespace PluginChatOverlay;

public sealed partial class Plugin
{
    private string PluginName(string id) => pluginNames.TryGetValue(id, out var name) ? name : id;
    private bool DrawTabSettings()
    {
        UiSection("タブ・ウィンドウ");
        var changed = false;
        if (settings.Tabs.Count < 8 && UiButton("タブを追加"))
        {
            var current = settings.Tabs.FirstOrDefault(t => t.Id == selectedSettingsTab) ?? settings.Tabs[0];
            var parent = settings.WindowOf(current);
            var number = 1; while (settings.Tabs.Any(t => t.Name == "タブ" + number)) number++;
            var tab = new ChatTab { Name = "タブ" + number, ParentWindowId = parent == "main" ? "" : parent, X = current.X + 40, Y = current.Y + 40 };
            if (parent != "main") settings.Tabs.First(t => t.Id == parent).Visible = true;
            settings.Visible = true;
            settings.Tabs.Add(tab); selectedSettingsTab = tab.Id; changed = true;
        }
        if (settings.Tabs.All(t => t.Id != selectedSettingsTab)) selectedSettingsTab = settings.Tabs[0].Id;
        if (ImGui.BeginCombo("編集するタブ", settings.Tabs.First(t => t.Id == selectedSettingsTab).Name))
        {
            try { foreach (var tab in settings.Tabs) if (ImGui.Selectable(tab.Name + "###configtab-" + tab.Id, tab.Id == selectedSettingsTab)) selectedSettingsTab = tab.Id; }
            finally { ImGui.EndCombo(); }
        }
        var selected = settings.Tabs.First(t => t.Id == selectedSettingsTab);
        var name = selected.Name; if (UiInputText("タブ名", ref name, 48)) { selected.Name = string.IsNullOrWhiteSpace(name) ? "チャット" : name; changed = true; }
        var detached = selected.Detached;
        if (UiCheckbox("独立ウィンドウとして表示", ref detached))
        {
            if (detached) { selected.Detached = true; selected.ParentWindowId = ""; }
            else settings.Attach(selected, settings.Tabs.FirstOrDefault(t => t.Detached && t.Id != selected.Id)?.Id ?? "main");
            selected.Visible = true; View(selected.Id).ResetGeometry = true; changed = true;
        }
        if (!selected.Detached)
        {
            var parent = settings.Tabs.FirstOrDefault(t => t.Detached && t.Id == selected.ParentWindowId);
            if (ImGui.BeginCombo("所属ウィンドウ", parent?.Name ?? "メイン"))
            {
                try
                {
                    if (ImGui.Selectable("メイン", parent == null)) { settings.Attach(selected, "main"); changed = true; }
                    foreach (var root in settings.Tabs.Where(t => t.Detached))
                        if (ImGui.Selectable(root.Name + "###parent-" + root.Id, root == parent))
                        { settings.Attach(selected, root.Id); root.Visible = true; changed = true; }
                }
                finally { ImGui.EndCombo(); }
            }
            ImGui.TextWrapped("追加タブは同じウィンドウ内に表示します。サイズ・固定・クリック透過は所属ウィンドウの設定を使用します。");
        }
        if (selected.Detached)
        {
            if (sizeEditingTab != selected.Id || !windowSizeEdited)
            {
                sizeEditingTab = selected.Id;
                editedWindowWidth = (int)MathF.Round(selected.Width);
                editedWindowHeight = (int)MathF.Round(selected.Height);
                windowSizeEdited = false;
            }
            ImGui.SetNextItemWidth(180);
            if (UiInputInt("幅 (px)", ref editedWindowWidth)) windowSizeEdited = true;
            ImGui.SetNextItemWidth(180);
            if (UiInputInt("高さ (px)", ref editedWindowHeight)) windowSizeEdited = true;
            if (UiButton("サイズを適用"))
            {
                selected.Width = editedWindowWidth = Math.Clamp(editedWindowWidth, 250, 4000);
                selected.Height = editedWindowHeight = Math.Clamp(editedWindowHeight, 180, 3000);
                View(selected.Id).RequestedSize = new(selected.Width, selected.Height);
                windowSizeEdited = false; changed = true;
            }
            ImGui.TextWrapped("幅250～4000px、高さ180～3000px。タイトルバー・タブを含むウィンドウ全体のサイズです。適用しても位置は変わりません。");
            var visible = selected.Visible; if (UiCheckbox("このウィンドウを表示", ref visible)) { selected.Visible = visible; changed = true; }
            var pinned = selected.Pinned;
            if (UiCheckbox("このウィンドウを固定", ref pinned)) { selected.Pinned = pinned; changed = true; }
            var pass = selected.ClickThrough;
            if (UiCheckbox("このウィンドウをクリック透過", ref pass)) { selected.ClickThrough = pass; changed = true; }
            if (UiButton("このウィンドウの最小化・固定・透過を解除"))
            {
                selected.Visible = true; selected.Pinned = false; selected.ClickThrough = false;
                if (windowModels.TryGetValue(selected.Id, out var host)) { host.Collapsed = false; host.CollapsedCondition = ImGuiCond.Always; host.RequestFocus = true; }
                changed = true;
            }
        }
        var all = selected.IncludeAll; if (UiCheckbox("すべてのプラグインを表示", ref all)) { selected.IncludeAll = all; changed = true; }
        if (!selected.IncludeAll)
        {
            var unknown = selected.IncludeUnknown;
            if (UiCheckbox("未分類ログを表示", ref unknown)) { selected.IncludeUnknown = unknown; changed = true; }
            ImGui.TextUnformatted("表示するプラグイン（複数選択可）");
            var child = ImGui.BeginChild("PluginChoices", new(Math.Min(ImGui.GetContentRegionAvail().X, 520 * ImGuiHelpers.GlobalScale), 190 * ImGuiHelpers.GlobalScale), true);
            try
            {
                if (child) foreach (var plugin in pluginNames.Keys.Concat(recognised).Distinct().OrderBy(PluginName))
                {
                    var included = selected.Plugins.Contains(plugin);
                    if (!UiCheckbox(PluginName(plugin) + "###include-" + plugin, ref included)) continue;
                    if (included) selected.Plugins.Add(plugin); else selected.Plugins.Remove(plugin); changed = true;
                }
            }
            finally { ImGui.EndChild(); }
        }
        ImGui.TextWrapped("タブの右クリックでもウィンドウ化できます。『所属ウィンドウ』でタブを別の窓へまとめられます。空の親ウィンドウは表示しません。移動後にマウスを離すと近い画面端・他のチャット窓へスナップします。");
        if (settings.Tabs.Count > 1 && UiButton("このタブを削除"))
        {
            var children = settings.Tabs.Where(t => t.ParentWindowId == selected.Id).ToArray();
            settings.RemoveTab(selected);
            foreach (var child in children) View(child.Id).ResetGeometry = true;
            views.Remove(selected.Id); selectedSettingsTab = settings.Tabs[0].Id; changed = true;
        }
        return changed;
    }
}
