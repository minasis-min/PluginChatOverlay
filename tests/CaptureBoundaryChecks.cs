using System.Reflection;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using PluginChatOverlay;

internal static class CaptureBoundaryChecks
{
    public static void Run()
    {
        var chat = DispatchProxy.Create<IChatGui, ServiceProxy>();
        var framework = DispatchProxy.Create<IFramework, ServiceProxy>();
        ((ServiceProxy)framework).Result = (method, _) => method.Name == "get_IsInFrameworkUpdateThread" ? true : null;
        var log = DispatchProxy.Create<IPluginLog, ServiceProxy>();
        var enabled = false;
        var history = new History();
        using var capture = new ChatCapture(chat, framework, log, history, () => enabled);
        Check(capture.Compatible, "capture boundary tests use pinned installed Dalamud with fake host services");
        var message = DispatchProxy.Create<IHandleableChatMessage, ServiceProxy>();
        var reads = 0;
        ((ServiceProxy)message).Result = (method, _) =>
        {
            reads++;
            if (method.Name == "get_LogKind") return XivChatType.Say;
            if (method.Name == "get_IsHandled") return false;
            throw new Exception("unexpected message access: " + method.Name);
        };
        var check = typeof(ChatCapture).GetMethod("Check", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Action<IHandleableChatMessage>>(capture);
        check(message); // Warm the delegate before measuring.
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) check(message);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(reads == 0 && capture.QueueMatches == 0 && capture.Passed == 0 && allocated < 100000,
            "disabled capture skips message processing and stack-trace allocation (1000 fake-host calls)");
        enabled = true; check(message);
        Check(capture.Passed == 1 && capture.Failures == 0 && reads == 1,
            "ordinary non-queue chat only reads its kind and preserves vanilla output");
        var removed = new List<string>();
        ((ServiceProxy)chat).Result = (method, _) =>
        {
            if (method.Name.StartsWith("remove_"))
            {
                removed.Add(method.Name);
                if (method.Name == "remove_CheckMessageHandled") throw new IOException("first unsubscribe failed");
            }
            return null;
        };
        capture.Dispose(); capture.Dispose();
        Check(removed.SequenceEqual(["remove_CheckMessageHandled"]),
            "capture disposal releases its sole subscription and is idempotent");
        var partialRemovals = new List<string>();
        ((ServiceProxy)chat).Result = (method, _) =>
        {
            if (method.Name == "add_CheckMessageHandled") throw new IOException("subscription failed");
            if (method.Name.StartsWith("remove_"))
            {
                partialRemovals.Add(method.Name);
                if (method.Name == "remove_CheckMessageHandled") throw new IOException("first rollback failed");
            }
            return null;
        };
        using var partial = new ChatCapture(chat, framework, log, new History(), () => true);
        Check(!partial.Compatible && partialRemovals.Count == 1,
            "partial chat initialization attempts rollback even if unsubscribe throws");
        using var module = Mono.Cecil.ModuleDefinition.ReadModule(typeof(ChatCapture).Assembly.Location);
        var captureType = module.Types.Single(t => t.FullName == typeof(ChatCapture).FullName);
        var calls = captureType.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions)
            .Select(i => i.Operand).OfType<Mono.Cecil.MethodReference>().ToArray();
        Check(!captureType.Fields.Any(f => f.Name == "pending")
            && !calls.Any(c => c.Name is "add_ChatMessageHandled" or "add_ChatMessageUnhandled"
                || c.DeclaringType.Name == "History" && c.Name == "Replace"),
            "capture has no cross-message pending ID or final-event history replacement path");
        var handlers = new Dictionary<string, Delegate>();
        var isolatedChat = DispatchProxy.Create<IChatGui, ServiceProxy>();
        ((ServiceProxy)isolatedChat).Result = (method, arguments) =>
        {
            if (method.Name.StartsWith("add_")) handlers[method.Name[4..]] = (Delegate)arguments![0]!;
            if (method.Name.StartsWith("remove_")) handlers.Remove(method.Name[7..]);
            return null;
        };
        var isolatedHistory = new History();
        using var isolatedCapture = new ChatCapture(isolatedChat, framework, log, isolatedHistory, () => true);
        var binding = BindingFlags.Static | BindingFlags.NonPublic;
        typeof(ChatCapture).GetField("origin", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(isolatedCapture,
            new QueueOrigin(typeof(CaptureCallProbe).GetMethod("Detour", binding)!, typeof(CaptureCallProbe).GetMethod("UpdateQueue", binding)!));
        var body = "message A"; var handled = false;
        var eventMessage = DispatchProxy.Create<IHandleableChatMessage, ServiceProxy>();
        ((ServiceProxy)eventMessage).Result = (method, _) => method.Name switch
        {
            "get_LogKind" => XivChatType.Say,
            "get_SourceKind" or "get_TargetKind" => default(XivChatRelationKind),
            "get_Timestamp" => 0,
            "get_Sender" => new Dalamud.Game.Text.SeStringHandling.SeString(),
            "get_Message" => new Dalamud.Game.Text.SeStringHandling.SeString(new Dalamud.Game.Text.SeStringHandling.Payloads.TextPayload(body)),
            "get_IsHandled" => handled,
            "PreventOriginal" => null,
            _ => throw new Exception(method.Name),
        };
        CaptureCallProbe.Call(() => ((IChatGui.OnHandleableChatMessageDelegate)handlers["CheckMessageHandled"])(eventMessage));
        // Host throws after capture A and skips final callbacks. B is hidden before Check.
        body = "message B"; handled = true;
        CaptureCallProbe.Call(() => { if (handlers.TryGetValue("ChatMessageHandled", out var final)) final.DynamicInvoke(eventMessage); });
        Check(isolatedHistory.Snapshot().Single().Plain == "message A" && isolatedCapture.Captured == 1,
            "skipped final event for A followed by early-hidden B never replaces A or captures B");
    }
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
}

internal static class CaptureCallProbe
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void Detour(Action callback) { callback(); GC.KeepAlive(callback); }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void UpdateQueue(Action callback) { Detour(callback); GC.KeepAlive(callback); }
    public static void Call(Action callback) => UpdateQueue(callback);
}
