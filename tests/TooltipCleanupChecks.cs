using PluginChatOverlay;
using Dalamud.Plugin.Services;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using System.Reflection;

internal static class TooltipCleanupChecks
{
    public static void Run()
    {
        var queue = new Queue<(Action Action, TimeSpan Delay)>();
        var errors = 0; var attempts = 0; var attached = true; var destroyed = false;
        var retry = new TooltipCleanupRetry(() =>
        {
            attempts++;
            if (attempts == 1) return false; // UI stage unavailable during logout.
            if (attempts == 2) throw new IOException("detach failed");
            attached = false;
            destroyed = true;
            return true;
        }, (action, delay) => queue.Enqueue((action, delay)), _ => errors++);
        retry.Request(); retry.Request();
        Check(retry.Pending && queue.Count == 1 && attempts == 0, "cleanup queues only one request and does not touch native state on caller thread");
        queue.Dequeue().Action();
        Check(retry.Pending && attached && !destroyed && queue.Peek().Delay == TimeSpan.FromMilliseconds(250),
            "unavailable UI retains attached anchor and schedules deferred cleanup");
        queue.Dequeue().Action();
        Check(retry.Pending && attached && !destroyed && queue.Peek().Delay == TimeSpan.FromMilliseconds(500),
            "detach failure cannot destroy registered node and retains retry state");
        queue.Dequeue().Action();
        Check(!retry.Pending && !attached && destroyed && queue.Count == 0 && attempts == 3,
            "cleanup survives logout and detach failure then stops scheduling after release");

        var ready = false; var terminal = false;
        var upgraded = new TooltipCleanupRetry(() => ready && terminal, (action, delay) => queue.Enqueue((action, delay)), _ => { });
        upgraded.Request(); terminal = true; upgraded.Request(); ready = true;
        queue.Dequeue().Action();
        Check(!upgraded.Pending && queue.Count == 0, "dispose upgrades an already pending hover release without duplicate tasks");

        var scheduleFails = true;
        var rescheduled = new TooltipCleanupRetry(() => true, (action, delay) =>
        {
            if (scheduleFails) throw new IOException("scheduler unavailable");
            queue.Enqueue((action, delay));
        }, _ => { });
        rescheduled.Request();
        Check(rescheduled.Pending && queue.Count == 0, "scheduler error preserves ownership for subsequent cleanup request");
        scheduleFails = false; rescheduled.Request(); queue.Dequeue().Action();
        Check(!rescheduled.Pending, "cleanup request can recover after scheduling failure");

        errors = 0; var failures = 0;
        var backoff = new TooltipCleanupRetry(() =>
        {
            if (++failures <= 10) throw new IOException("temporary failure");
            return true;
        }, (action, delay) => queue.Enqueue((action, delay)), _ => { errors++; throw new IOException("logger failure"); });
        backoff.Request(immediate: true);
        var maxDelay = TimeSpan.Zero;
        while (queue.Count > 0)
        {
            var next = queue.Dequeue(); if (next.Delay > maxDelay) maxDelay = next.Delay;
            next.Action();
        }
        Check(!backoff.Pending && failures == 11 && errors == 1 && maxDelay == TimeSpan.FromSeconds(5),
            "retries back off, contain reporting failures and do not emit a warning every tick");

        var framework = DispatchProxy.Create<IFramework, ServiceProxy>();
        ((ServiceProxy)(object)framework).Result = (method, args) =>
        {
            if (method.Name == "get_IsInFrameworkUpdateThread") return false;
            if (method.Name == "RunOnTick") { queue.Enqueue(((Action)args![0]!, (TimeSpan)args[1]!)); return Task.CompletedTask; }
            return null;
        };
        var lifecycle = DispatchProxy.Create<IAddonLifecycle, ServiceProxy>();
        var removes = 0;
        ((ServiceProxy)(object)lifecycle).Result = (method, args) =>
        {
            if (method.Name == "UnregisterListener" && ++removes == 1) throw new IOException("listener removal failed");
            return null;
        };
        var native = new NativeItemTooltip(DispatchProxy.Create<IGameGui, ServiceProxy>(), DispatchProxy.Create<IPluginLog, ServiceProxy>(), framework);
        native.Register(lifecycle, DispatchProxy.Create<IGameConfig, ServiceProxy>());
        native.Unregister(); native.Unregister();
        Check(queue.Count == 1 && removes == 0 && native.Status.Contains("再試行"),
            "native dispose preserves listener state and schedules framework cleanup once");
        queue.Dequeue().Action();
        Check(removes == 1 && queue.Count == 1 && native.Status.Contains("再試行"),
            "native listener-unsubscribe failure remains recoverable after plugin dispose");
        queue.Dequeue().Action();
        Check(removes == 2 && queue.Count == 0 && !native.Status.Contains("再試行"),
            "native listener cleanup completes on retry without a permanent event subscription");
        Check(!native.Show(new(100, Dalamud.Utility.ItemKind.Normal)),
            "disposed backend refuses new tooltip requests before touching game state");
    }
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
}
