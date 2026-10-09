using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace PluginChatOverlay;

public sealed partial class Plugin
{
    private readonly WindowSystem chatWindows = new("PluginChatOverlay");
    private readonly Dictionary<string, ChatWindow> windowModels = [];
    private DisplayDocument[] frameDocuments = [];

    private void DrawStandardWindows(DisplayDocument[] docs)
    {
        frameDocuments = docs;
        foreach (var id in windowModels.Keys.Where(id => id != "main" && settings.Tabs.All(t => t.Id != id)).ToArray())
        {
            chatWindows.RemoveWindow(windowModels[id]); windowModels.Remove(id);
        }
        if (!windowModels.ContainsKey("main")) AddChatWindow("main", null);
        foreach (var tab in settings.Tabs)
            if (tab.Detached && !windowModels.ContainsKey(tab.Id)) AddChatWindow(tab.Id, tab);
        foreach (var window in windowModels.Values)
        {
            window.RequestedOpen = settings.Visible && (window.Tab == null
                ? settings.WindowTabs("main").Any()
                : window.Tab.Detached && window.Tab.Visible);
            window.IsOpen = window.RequestedOpen;
        }
        chatWindows.Draw();
        var changed = false;
        foreach (var window in windowModels.Values.Where(w => w.RequestedOpen && !w.IsOpen))
        {
            if (window.Tab == null) settings.Visible = false; else window.Tab.Visible = false;
            changed = true;
        }
        if (changed) Save();
    }
    private void AddChatWindow(string id, ChatTab? tab)
    {
        var window = new ChatWindow(this, id, tab);
        windowModels.Add(id, window); View(id).Host = window; chatWindows.AddWindow(window);
    }
    private sealed class ChatWindow : Window
    {
        private readonly Plugin owner;
        private readonly string id;
        private bool stylesPushed;
        public ChatTab? Tab { get; }
        public bool RequestedOpen;
        public ChatWindow(Plugin owner, string id, ChatTab? tab)
            : base(tab == null ? WindowTitle.MainTitle(owner.settings.ShowWindowTitle) : WindowTitle.Detached(tab.Name, tab.Id, 0), ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings, true)
        {
            this.owner = owner; this.id = id; Tab = tab;
            AllowPinning = true; AllowClickthrough = true; AllowBackgroundBlur = true;
            DisableFadeInFadeOut = true;
            RespectCloseHotkey = true;
        }
        public override bool DrawConditions()
        {
            var visible = !owner.disposed && !owner.uiFault
                && (!owner.settings.HideLoggedOut || owner.clientState.IsLoggedIn)
                && (!owner.settings.HideDuringCutscene || !owner.pi.UiBuilder.CutsceneActive);
            if (!visible) owner.View(id).HeaderDragging = false;
            return visible;
        }
        public override void PreDraw()
        {
            WindowName = Tab == null ? WindowTitle.MainTitle(owner.settings.ShowWindowTitle) : WindowTitle.Detached(Tab.Name, Tab.Id, owner.View(id).Unread);
            IsPinned = Tab?.Pinned ?? owner.settings.Pinned;
            IsClickthrough = Tab?.ClickThrough ?? owner.settings.ClickThrough;
            Flags = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings
                | (owner.settings.AllowResize ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoResize)
                | (owner.settings.ShowWindowTitle ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoTitleBar);
            if (!owner.settings.ShowWindowTitle) { Collapsed = false; CollapsedCondition = ImGuiCond.Always; }
            owner.ApplyHeaderDrag(id);
            if (Tab == null && owner.resetGeometry)
            {
                ImGui.SetNextWindowPos(owner.settings.Position, ImGuiCond.Always);
                ImGui.SetNextWindowSize(owner.settings.Size, ImGuiCond.Always); owner.resetGeometry = false;
            }
            else if (Tab != null && owner.View(id).ResetGeometry)
            {
                ImGui.SetNextWindowPos(new(Tab.X, Tab.Y), ImGuiCond.Always);
                ImGui.SetNextWindowSize(new(Tab.Width, Tab.Height), ImGuiCond.Always); owner.View(id).ResetGeometry = false;
            }
            if (Tab != null && owner.View(id).RequestedSize is { } requestedSize)
            {
                ImGui.SetNextWindowSize(requestedSize, ImGuiCond.Always);
                owner.View(id).RequestedSize = null;
            }
            owner.PushChatBackground(); stylesPushed = true;
        }
        public override void Draw()
        {
            try
            {
                if (Tab == null) owner.DrawMainWindow(owner.frameDocuments);
                else owner.DrawDetached(Tab, owner.frameDocuments);
            }
            catch (Exception ex)
            {
                owner.log.Error(ex, "Standard chat window drawing failed; separation suspended");
                owner.uiFault = true; owner.configOpen = true;
            }
        }
        public override void PostDraw()
        {
            if (stylesPushed) { ImGui.PopStyleColor(4); stylesPushed = false; }
            Collapsed = null;
            var pinned = Tab?.Pinned ?? owner.settings.Pinned;
            var pass = Tab?.ClickThrough ?? owner.settings.ClickThrough;
            if (pinned == IsPinned && pass == IsClickthrough) return;
            if (Tab == null) { owner.settings.Pinned = IsPinned; owner.settings.ClickThrough = IsClickthrough; }
            else { Tab.Pinned = IsPinned; Tab.ClickThrough = IsClickthrough; }
            owner.Save();
        }
    }
}
