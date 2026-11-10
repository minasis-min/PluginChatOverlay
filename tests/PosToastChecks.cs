using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using PluginChatOverlay;
internal static class PosToastChecks
{
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("PASS " + label); }
    public static void Run()
    {
        var plain = new SeStringBuilder().AddText("エリア名 (12.3, 4.5)").Build().Encode();
        var linked = new SeStringBuilder().AddText("地点 ").Add(new MapLinkPayload(1, 1, 100, 200)).Build().Encode();
        Check(!PositionMessages.HasMapLink(plain) && PositionMessages.HasMapLink(linked), "POS copy uses structured map payloads rather than coordinate-like text");
        Check(PositionMessages.IsPlayerKind((int)XivChatType.Say) && !PositionMessages.IsPlayerKind((int)XivChatType.Echo), "POS-only mode excludes plugin notification channels");
        var rows = new[] { new GameLogFilterEntry(1, "Say", 10, ushort.MaxValue, ushort.MaxValue, 1, 1) };
        var catalog = new GameLogCatalog(rows); var filter = new GameLogFilter { Enabled = false, ShowMapMessages = true, Rows = [] };
        var plan = catalog.BuildSelection([filter]);
        Check(plan.Matches(10, 0, 0) && !plan.MatchesBody(10, 0, 0, plain) && plan.MatchesBody(10, 0, 0, linked), "map-message opt-in works with regular game display disabled and no category selected");
        var full = new GameLogFilter { Enabled = true, Rows = [1] };
        Check(catalog.BuildSelection([filter, full]).MatchesBody(10, 0, 0, plain), "regular game categories and independent map-message opt-in are additive");
        Check(catalog.BuildSelection([filter], false).MatchesBody((int)XivChatType.FreeCompany,0,0,linked)
            && !catalog.BuildSelection([full],false).HasSelection, "map statements bypass the regular-game master switch while ordinary categories do not");
        var saved = SettingsStore.Parse(System.Text.Json.JsonSerializer.Serialize(new Settings { Tabs = [new() { GameLogs = filter }] }));
        Check(saved.Tabs[0].GameLogs.ShowMapMessages && !new Settings().SeparatePluginToasts, "POS choice persists and new toast separation defaults off");
        var toastSettings = SettingsStore.Parse(System.Text.Json.JsonSerializer.Serialize(new Settings { ToastBackgroundColor = 0x41332211 }));
        Check(toastSettings.ToastBackgroundColor == 0x41332211 && SettingsStore.Parse("{}").ToastBackgroundColor == Settings.DefaultToastBackgroundColor,
            "toast background RGBA persists and old settings receive the default without enabling capture");
        Check(SettingsStore.Parse("{\"ToastFontSize\":999}").ToastFontSize == 48 && SettingsStore.Parse("{\"ToastFontSize\":0}").ToastFontSize == 16
            && SettingsStore.Parse("{\"ToastFontSize\":28}").ToastFontSize == 28, "toast font pixel size persists and invalid sizes are clamped");
        var raw = new RawPayload([2, 0x27, 2, 0x61, 3]);
        var customLink = new DalamudLinkPayload();
        typeof(DalamudLinkPayload).GetProperty("Plugin")!.SetValue(customLink, "AetheryteLinkInChat");
        typeof(DalamudLinkPayload).GetProperty("CommandId")!.SetValue(customLink, 1u);
        var rich = new SeStringBuilder().Add(customLink).AddText("Lifestream").Add(raw).Add(RawPayload.LinkTerminator).Build();
        var record = new ChatRecord(1, 10, 0, 0, 0, [], rich.Encode(), "Lifestream") { Origin = ChatRecordOrigin.Game };
        var content = DisplayDocument.Content(record, new Settings { ShowTime = false }, []);
        var envelope = Links.Envelope(content, 0);
        Check(content.AsSpan().SequenceEqual(rich.Encode()) && envelope.Payloads.OfType<RawPayload>().Any(p => p.Encode().AsSpan().SequenceEqual(raw.Encode())),
            "overlay presentation preserves opaque destination payloads inside Aetheryte and Lifestream links");
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var detour = typeof(PosToastChecks).GetMethod(nameof(Detour), flags)!;
        var show = typeof(PosToastChecks).GetMethod(nameof(Show), flags)!;
        var update = typeof(PosToastChecks).GetMethod(nameof(Update), flags)!;
        var origin = new ToastOrigin(detour, show, update);
        Check(origin.Matches([detour, show, update]) && !origin.Matches([detour, show])
            && !origin.Matches([detour, update]) && !origin.Matches([detour, show, update, detour]), "toast origin requires exact queue path and rejects native boundaries and recursion");
        runtimeOrigin = origin; Check(Update() && !Show(), "optimized runtime toast stack distinguishes queue calls from a direct native-style entry");
        Check(ToastOrigin.Create() != null, "pinned host toast method contracts match without invoking native game functions");
        Capture();
        long now = 1; var queue = new ToastQueue(() => now); var bytes = linked.ToArray();
        Check(queue.TryAdd(bytes, QuestToastPosition.Centre, 111), "toast queue accepts bounded owned content"); bytes[0] ^= 1;
        Check(queue.Snapshot().Single().Bytes.AsSpan().SequenceEqual(linked), "toast queue content is independent of caller storage");
        now += 1000;
        var second = new SeStringBuilder().AddText("B notification").Build().Encode();
        Check(queue.TryAdd(second, QuestToastPosition.Centre, 111) && queue.Snapshot().Length == 2
            && queue.Snapshot()[0].Bytes.AsSpan().SequenceEqual(linked) && queue.Snapshot()[1].Bytes.AsSpan().SequenceEqual(second),
            "new toast appends below the first without overwriting it");
        now = 1 + ToastQueue.DurationMs;
        Check(queue.Snapshot().Length == 1 && queue.Snapshot()[0].Bytes.AsSpan().SequenceEqual(second),
            "second toast becomes first when the older toast expires and retains its own lifetime");
        for (var i = 1; i < ToastQueue.Capacity; i++) Check(queue.TryAdd(linked, QuestToastPosition.Centre, 111), "bounded toast stack accepts remaining capacity");
        Check(!queue.TryAdd(linked, QuestToastPosition.Centre, 111) && queue.Snapshot().Length == ToastQueue.Capacity
            && queue.Snapshot()[0].Bytes.AsSpan().SequenceEqual(second), "full toast stack refuses interception without losing existing banners");
        now += ToastQueue.DurationMs; Check(queue.Snapshot().Length == 0, "expired toasts disappear without any history persistence");
        queue.TryAdd(linked, QuestToastPosition.Centre, 111); queue.Clear(); Check(queue.Snapshot().Length == 0, "hiding or disabling clears pending toast presentation");
    }
    private static ToastOrigin runtimeOrigin = null!;
    [MethodImpl(MethodImplOptions.NoInlining)] private static bool Detour() => runtimeOrigin.IsQueueCall();
    [MethodImpl(MethodImplOptions.NoInlining)] private static bool Show() { var result = Detour(); GC.KeepAlive(runtimeOrigin); return result; }
    [MethodImpl(MethodImplOptions.NoInlining)] private static bool Update() { var result = Show(); GC.KeepAlive(runtimeOrigin); return result; }
    private static void Capture()
    {
        var toast = DispatchProxy.Create<IToastGui, ServiceProxy>();
        IToastGui.OnQuestToastDelegate? callback = null; var registrations = 0;
        ((ServiceProxy)toast).Result = (method, args) =>
        {
            if (method.Name == "add_QuestToast") { callback += (IToastGui.OnQuestToastDelegate)args![0]!; registrations++; }
            if (method.Name == "remove_QuestToast") { callback -= (IToastGui.OnQuestToastDelegate)args![0]!; registrations--; }
            return null;
        };
        var ready = false; var onThread = true; var queued = true; var accepting = true; var throws = false; var accepted = 0;
        using var capture = new QuestToastCapture(toast, () => onThread, () => ready, (bytes, _, _) => { if (throws) throw new Exception(); if (accepting) accepted++; return accepting; }, () => queued);
        var text = new SeStringBuilder().AddText("通知").Build(); var options = new QuestToastOptions();
        bool Emit(bool previous = false) { var handled = previous; callback!(ref text, ref options, ref handled); return handled; }
        Check(!Emit() && accepted == 0, "disabled or non-rendering PCO leaves the native toast intact"); ready = true;
        queued = false; Check(!Emit(), "game-origin and unproven toast paths are never suppressed"); queued = true;
        onThread = false; Check(!Emit(), "toast capture rejects foreign threads"); onThread = true;
        accepting = false; Check(!Emit(), "renderer refusal preserves original toast"); accepting = true;
        options.PlaySound = true; Check(!Emit(), "unsupported sound behavior passes through"); options.PlaySound = false;
        options.DisplayCheckmark = true; Check(!Emit(), "unsupported checkmarks keep native rendering"); options.DisplayCheckmark = false;
        throws = true; Check(!Emit() && capture.Failures == 1, "capture exception leaves native toast unhandled"); throws = false;
        Check(Emit() && accepted == 1 && capture.Captured == 1, "original toast is suppressed only after successful capture");
        Check(Emit(true) && accepted == 1, "already handled toasts are not resurrected or copied");
        capture.Suspend("test renderer failure"); capture.Dispose(); Check(registrations == 0 && callback == null, "toast events unsubscribe exactly once on unload");
    }
}
