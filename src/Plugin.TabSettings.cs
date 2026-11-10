using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;

namespace PluginChatOverlay;

public sealed partial class Plugin
{
    private string PluginName(string id) => pluginNames.TryGetValue(id, out var name) ? name : id;
    private static readonly string[] TabSettingsPages=["管理","プラグイン通知","ゲームログ","ウィンドウ"];
    private bool DrawTabSettings()
    {
        if(settings.Tabs.All(t=>t.Id!=selectedSettingsTab)) selectedSettingsTab=settings.Tabs[0].Id;
        var changed=SettingsCard("tab-target",DrawTabTarget);
        if(minasisFrame) return changed | DrawTabSettingsPage(workspace.Tab);
        if(!ImGui.BeginTabBar("ChatTabSettingsPages")) return changed;
        try
        {
            for(var i=0;i<TabSettingsPages.Length;i++)
            {
                var active=ImGui.BeginTabItem(TabSettingsPages[i],ImGuiTabItemFlags.NoTooltip);
                var available=ImGui.GetItemRectMax().X-ImGui.GetItemRectMin().X-ImGui.GetStyle().FramePadding.X*2;
                if(ImGui.CalcTextSize(TabSettingsPages[i]).X>available) UiTooltip(TabSettingsPages[i]);
                if(!active) continue;
                try { changed |= DrawTabSettingsPage(i); }
                finally { ImGui.EndTabItem(); }
            }
        }
        finally { ImGui.EndTabBar(); }
        return changed;
    }
    private bool DrawTabTarget()
    {
        var choices=settings.Tabs.Select(t=>new MinasisUI.UiChoice(t.Id,t.Name)).ToArray();
        var selectedIndex=settings.Tabs.FindIndex(t=>t.Id==selectedSettingsTab);
        if(UiDropdown("edit-chat-tab","編集するタブ",ref selectedIndex,choices,280 * ImGuiHelpers.GlobalScale)) selectedSettingsTab=choices[selectedIndex].Id;
        return false;
    }
    private bool DrawTabSettingsPage(int page) => page switch
    {
        0 => SettingsCard("tab-details",DrawTabBasics),
        1 => SettingsCard("tab-plugins",()=>DrawPluginLogFilters(settings.Tabs.First(t=>t.Id==selectedSettingsTab))),
        2 => SettingsCard("tab-game",()=>DrawGameLogFilters(settings.Tabs.First(t=>t.Id==selectedSettingsTab))),
        3 => SettingsCard("tab-window",DrawSelectedWindowSettings) | SettingsCard("tab-order",DrawTabOrderSettings),
        _ => false,
    };
    private bool DrawTabBasics()
    {
        UiSection("タブ");
        if (scrollChatTabSettings) { ImGui.SetScrollHereY(0); scrollChatTabSettings = false; }
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
        var selected = settings.Tabs.First(t => t.Id == selectedSettingsTab);
        var name = selected.Name; if (UiInputText("タブ名", ref name, 48)) { selected.Name = string.IsNullOrWhiteSpace(name) ? "チャット" : name; changed = true; }
        UiDescription("窓の分離・所属先・サイズは「ウィンドウ」で設定します。タブの右クリックからも分離できます。");
        if (settings.Tabs.Count > 1 && UiGuardedButton("このタブを削除"))
        {
            var children = settings.Tabs.Where(t => t.ParentWindowId == selected.Id).ToArray();
            settings.RemoveTab(selected);
            foreach (var child in children) View(child.Id).ResetGeometry = true;
            views.Remove(selected.Id); selectedSettingsTab = settings.Tabs[0].Id; changed = true;
        }
        return changed;
    }
    private bool MoveSelectedTab(int direction)
    {
        if(!settings.MoveTab(selectedSettingsTab,direction)) return false;
        var tab=settings.Tabs.First(t=>t.Id==selectedSettingsTab);
        View(settings.WindowOf(tab)).ResetTabOrder=true;
        return true;
    }
    private bool DrawTabOrderSettings()
    {
        UiSection("タブの順番");
        var selected=settings.Tabs.First(t=>t.Id==selectedSettingsTab);
        var tabs=settings.WindowTabs(settings.WindowOf(selected)).ToArray();
        foreach(var tab in tabs)
        {
            var clicked=minasisFrame ? ui.Selectable("tab-order-"+tab.Id,tab.Name,tab.Id==selectedSettingsTab)
                : ImGui.Selectable(tab.Name+"###tab-order-"+tab.Id,tab.Id==selectedSettingsTab);
            if(clicked) selectedSettingsTab=tab.Id;
        }
        var index=Array.FindIndex(tabs,t=>t.Id==selectedSettingsTab);
        var changed=false;
        ImGui.BeginDisabled(index<=0);
        try { if(UiButton("前へ")) changed |= MoveSelectedTab(-1); }
        finally { ImGui.EndDisabled(); }
        ImGui.SameLine();
        ImGui.BeginDisabled(index<0 || index>=tabs.Length-1);
        try { if(UiButton("後ろへ")) changed |= MoveSelectedTab(1); }
        finally { ImGui.EndDisabled(); }
        return changed;
    }
    private bool DrawPluginLogFilters(ChatTab selected)
    {
        UiSection("プラグイン通知");
        var changed = false;
        var all = selected.IncludeAll; if (UiCheckbox("すべてのプラグインを表示", ref all)) { selected.IncludeAll = all; changed = true; }
        if (!selected.IncludeAll)
        {
            var unknown = selected.IncludeUnknown;
            if (UiCheckbox("プラグイン名を判別できない通知を表示", ref unknown)) { selected.IncludeUnknown = unknown; changed = true; }
            ImGui.TextUnformatted("表示するプラグイン（複数選択可）");
            var child = ImGui.BeginChild("PluginChoices", new(Math.Min(ImGui.GetContentRegionAvail().X, 520 * ImGuiHelpers.GlobalScale), 190 * ImGuiHelpers.GlobalScale), true);
            try
            {
                if (child) foreach (var plugin in pluginNames.Keys.Concat(recognised).Distinct().OrderBy(PluginName))
                {
                    var included = selected.Plugins.Contains(plugin);
                    if (!UiListCheckbox(PluginName(plugin) + "###include-" + plugin, ref included)) continue;
                    if (included) selected.Plugins.Add(plugin); else selected.Plugins.Remove(plugin); changed = true;
                }
            }
            finally { ImGui.EndChild(); }
        }
        return changed;
    }
    private bool DrawSelectedWindowSettings()
    {
        UiSection("個別ウィンドウ");
        var changed = false;
        if (settings.Tabs.All(t => t.Id != selectedSettingsTab)) selectedSettingsTab = settings.Tabs[0].Id;
        var selected = settings.Tabs.First(t => t.Id == selectedSettingsTab);
        var detached = selected.Detached;
        if (UiCheckbox("独立ウィンドウとして表示", ref detached))
        {
            if (detached) { selected.Detached = true; selected.ParentWindowId = ""; }
            else settings.Attach(selected, settings.Tabs.FirstOrDefault(t => t.Detached && t.Id != selected.Id)?.Id ?? "main");
            selected.Visible = true; View(selected.Id).ResetGeometry = true; changed = true;
        }
        if (!selected.Detached)
        {
            var roots=settings.Tabs.Where(t=>t.Detached).ToArray();
            var parents=new[] { new MinasisUI.UiChoice("main","メイン") }.Concat(roots.Select(t=>new MinasisUI.UiChoice(t.Id,t.Name))).ToArray();
            var parentIndex=Array.FindIndex(parents,c=>c.Id==settings.WindowOf(selected));
            if(UiDropdown("tab-parent-window","所属ウィンドウ",ref parentIndex,parents,280 * ImGuiHelpers.GlobalScale))
            {
                settings.Attach(selected,parents[parentIndex].Id); changed=true;
            }
            UiDescription("サイズ・固定・クリック透過は、所属ウィンドウの設定に従います。");
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
            UiDescription("幅250～4000px、高さ180～3000px。タイトルバー・タブを含むサイズです。位置は変わりません。");
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
        return changed;
    }
}
