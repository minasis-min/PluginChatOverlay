using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace PluginChatOverlay;

public sealed partial class Plugin : IDalamudPlugin
{
    private const string Command = "/pcoverlay";
    private readonly IDalamudPluginInterface pi;
    private readonly ICommandManager commands;
    private readonly IChatGui chat;
    private readonly IPluginLog log;
    private readonly IFramework framework;
    private readonly IKeyState keyState;
    private readonly IClientState clientState;
    private readonly IGameGui game;
    private readonly CopyInput copyInput = new();
    private volatile bool chatFocused, controlDown, copyKeyDown;
    private long inputUpdatedAt;
    private ViewState? focusedView;
    private bool resetConfigSize = true;
    private readonly SettingsStore store;
    private readonly Settings settings;
    private readonly History history = new();
    private readonly ChatCapture capture;
    private readonly GameLogCapture gameCapture;
    private readonly GameLogCatalog gameLogCatalog;
    private volatile GameLogSelection gameCopySelection = GameLogSelection.Empty;
    private readonly Links links;
    private readonly Dictionary<long, DisplayDocument> documents = [];
    private readonly Dictionary<string, ViewState> views = [];
    private readonly Dictionary<string, string> pluginNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> recognised = new(StringComparer.Ordinal);
    private readonly PluginTags tags = new();
    private readonly FragmentLayout fragmentLayout = new();
    private readonly ChatPalette palette;
    private bool configOpen, resetGeometry = true, disposed, uiFault;
    private long geometryDirtyAt;
    private int presentationRevision;
    private string saveNotice = "";
    private bool testLinkActive;
    private bool commandRegistered;
    private long testLinkClicks;
    private volatile bool pluginsChanged = true;

    public Plugin(IDalamudPluginInterface pi, ICommandManager commands, IChatGui chat,
        IFramework framework, IGameGui game, IDataManager data, IKeyState keyState, IClientState clientState, IPluginLog log,
        IAddonLifecycle addonLifecycle, IGameConfig gameConfig, ISeStringEvaluator seStringEvaluator)
    {
        this.pi = pi; this.commands = commands; this.chat = chat; this.log = log;
        this.framework = framework; this.keyState = keyState;
        this.clientState = clientState; this.game = game;
        var assemblyDirectory = pi.AssemblyLocation.DirectoryName ?? throw new IOException("DLL配置先不明");
#if DISTRIBUTION
        store = SettingsStore.ForDistribution(pi.ConfigDirectory.FullName, assemblyDirectory);
#else
        store = new(assemblyDirectory);
#endif
        try { settings = store.Load(); }
        catch (Exception ex) { settings = new(); saveNotice = "設定読込失敗。既定値で起動しました。"; log.Error(ex, "Settings load failed"); }
        history.ApplyLimit(settings.HistoryLimit);
        history.ApplyGameLimit(settings.GameHistoryLimit);
        gameLogCatalog = GameLogCatalog.Load(data, log);
        UpdateGameLogSelection();
        links = new(chat, game, log, framework);
        palette = new(data, log);
        try
        {
            headingFonts = new(pi.UiBuilder.FontAtlas);
            headingFonts.Apply(ui);
            ApplyUiVisibility();
            RebuildChatFont();
            commandRegistered = commands.AddHandler(Command, new CommandInfo(OnCommand) { HelpMessage = "Plugin Chat Overlay: show / config / on / off / reset / test" });
            if (!commandRegistered) throw new InvalidOperationException(Command + " は既に使用されています。");
            capture = new(chat, framework, log, history, () => settings.Enabled && !uiFault);
            var gameFormatter = new GameLogFormatter(data, seStringEvaluator);
            gameCapture = new(chat, framework, log, history,
                () => settings.CopyGameLogs && gameLogCatalog.Available && capture.Compatible && !uiFault,
                capture.ClassifyCurrentCall, WantsGameLog, gameFormatter.Format);
            links.InitializeTooltips(addonLifecycle, gameConfig);
            pi.UiBuilder.Draw += Draw;
            pi.UiBuilder.HideUi += StopWindowDrags;
            pi.UiBuilder.OpenConfigUi += OpenConfig;
            pi.UiBuilder.OpenMainUi += OpenMain;
            pi.ActivePluginsChanged += PluginsChanged;
            framework.Update += SuppressGameCopy;
        }
        catch { Dispose(); throw; }
    }
    private void PluginsChanged(IActivePluginsChangedEventArgs _) => pluginsChanged = true;
    private void RefreshPlugins()
    {
        if (!pluginsChanged) return;
        pluginsChanged = false;
        var installed = pi.InstalledPlugins.ToArray();
        pluginNames.Clear();
        foreach (var plugin in installed) pluginNames[plugin.InternalName] = plugin.Name;
        tags.Reset(pluginNames.Select(p => (p.Key, p.Value)));
        presentationRevision++;
    }
    private void OpenConfig() => configOpen = true;
    private void StopWindowDrags()
    {
        try { links.CloseTooltip(); }
        finally
        {
            foreach (var view in views.Values) { view.HeaderDragging = view.AltDragging = view.ScrollbarDragging = view.ScrollbarHovered = false; view.PendingAltFrame = -1; view.Selection.EndDrag(); }
            chatFocused = false; focusedView = null;
            copyInput.CancelRequest(); inputUpdatedAt = 0;
        }
    }
    private ChatTab? PrimaryWindowTab => settings.WindowOf(settings.Tabs[0]) is var id && id != "main" ? settings.Tabs.FirstOrDefault(t => t.Id == id) : null;
    private void OpenMain() { settings.Visible = true; if (PrimaryWindowTab is { } root) root.Visible = true; else settings.MainWindowVisible = true; Save(); }
    private void Save()
    {
        try { UpdateGameLogSelection(); store.Save(settings); saveNotice = ""; }
        catch (Exception ex) { saveNotice = "設定を保存できませんでした。"; log.Error(ex, "Settings save failed"); }
    }
    private void Draw()
    {
        lock (renderCacheGate) DrawCore();
    }
    private void DrawCore()
    {
        if (disposed) return;
        links.BeginTooltipFrame();
        try
        {
            chatFocused = false;
            focusedView = null;
            PruneHistoryCaches();
            controlDown = ImGui.GetIO().KeyCtrl;
            copyKeyDown = ImGui.IsKeyDown(ImGuiKey.C);
            inputUpdatedAt = Environment.TickCount64;
            RefreshPlugins();
            UpdateFontChooser();
            using var selectedTheme = BeginUiTheme();
            if (chatHiddenByNativeExpansion && !nativeChatWasHidden) StopWindowDrags();
            nativeChatWasHidden = chatHiddenByNativeExpansion;
            if (!chatHiddenByNativeExpansion) DrawNativeChatButtons();
            using (chatFont?.Push())
                if (!chatHiddenByNativeExpansion && !uiFault && settings.Visible && (!settings.HideLoggedOut || clientState.IsLoggedIn) && (!settings.HideDuringCutscene || !pi.UiBuilder.CutsceneActive)) DrawChatWindows();
            if (configOpen) DrawConfig();
            CompleteCopyRequest();
            if (geometryDirtyAt != 0 && Environment.TickCount64 - geometryDirtyAt > 650 && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
            { geometryDirtyAt = 0; Save(); }
        }
        catch (Exception ex)
        {
            if (!uiFault) log.Error(ex, "Overlay drawing failed; separation suspended");
            uiFault = true; configOpen = true;
        }
        finally
        {
            var viewport = ImGui.GetMainViewport();
            links.SetTooltipWindows(windowModels.Where(p => !chatHiddenByNativeExpansion && p.Value.IsOpen).Select(p => View(p.Key).Window)
                .Where(r => r != null).Select(r => r!.Value).ToArray(), viewport.Pos, viewport.Size);
            if (uiFault || !clientState.IsLoggedIn || !settings.LinkTooltips) links.BeginTooltipFrame();
            links.EndTooltipFrame();
        }
    }
    private void UpdateGameLogSelection() => gameCopySelection = gameLogCatalog.BuildSelection(settings.Tabs.Select(t => t.GameLogs));
    private bool WantsGameLog(int kind, int source, int target) => gameCopySelection.Matches(kind, source, target);
    private string Plain(ChatRecord record) => (settings.ShowTime ? ChatClock.Format(record, settings.EffectivePadTimestampHour) + " " : "") + record.Plain;
    public void Dispose()
    {
        lock (renderCacheGate) DisposeCore();
    }
    private void DisposeCore()
    {
        if (disposed) return;
        disposed = true;
        Cleanup.Run(ex => log.Warning(ex, "Plugin cleanup failed; remaining resources will still be released"),
            () => framework.Update -= SuppressGameCopy,
            () => pi.UiBuilder.Draw -= Draw,
            () => pi.UiBuilder.OpenConfigUi -= OpenConfig,
            () => pi.UiBuilder.OpenMainUi -= OpenMain,
            () => pi.UiBuilder.HideUi -= StopWindowDrags,
            () => pi.ActivePluginsChanged -= PluginsChanged,
            () => gameCapture?.Dispose(),
            () => capture?.Dispose(),
            () => links.DisposeTooltips(),
            () => fontChooser?.Dispose(),
            () => chatFont?.Dispose(),
            () => fontAtlas?.Dispose(),
            () => ui.Dispose(),
            () => headingFonts?.Dispose(),
            () => { if (commandRegistered) { commands.RemoveHandler(Command); commandRegistered = false; } },
            () => { if (testLinkActive) chat.RemoveChatLinkHandler(701); },
            () => { if (geometryDirtyAt != 0) Save(); },
            () => history.Clear(),
            () => documents.Clear(),
            () => views.Clear(),
            () => chatWindows.RemoveAllWindows(),
            () => { windowModels.Clear(); frameDocuments = []; renderedDocuments = []; renderedSnapshot = null; });
    }
}
