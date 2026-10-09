using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using PluginChatOverlay;

internal static class TooltipChecks
{
    public static void Run()
    {
        var backend = new Backend(); var hover = new TooltipHover(backend);
        var normal = TooltipItem.From(new ItemPayload(100u));
        var hq = TooltipItem.From(new ItemPayload(100u, ItemKind.Hq));
        var key = TooltipItem.From(new ItemPayload(2000001u, ItemKind.EventItem));
        Check(normal.RawId == 100 && hq.RawId == 1000100 && hq.Kind == ItemKind.Hq && key.RawId == 2000001 && key.Kind == ItemKind.EventItem, "native tooltip preserves raw HQ and event item identifiers");
        hover.BeginFrame(); hover.EndFrame();
        Check(backend.Events.Count == 0, "non-item or absent hover never opens native tooltip");
        hover.BeginFrame(); hover.Request(normal); hover.EndFrame();
        hover.BeginFrame(); hover.Request(normal); hover.EndFrame();
        Check(backend.Events.SequenceEqual(["show:100"]), "continuous hover does not reopen native addon every frame");
        hover.BeginFrame(); hover.Request(hq); hover.EndFrame();
        Check(backend.Events.TakeLast(2).SequenceEqual(["hide:100", "show:1000100"]), "changing hovered item closes old native tooltip before opening new one");
        hover.BeginFrame(); hover.EndFrame(); hover.Close();
        Check(backend.Events.Last() == "hide:1000100" && backend.Events.Count == 4, "leaving hover closes owned tooltip once");
        hover.BeginFrame(); hover.Request(key); hover.EndFrame(); hover.Close(); hover.Close();
        Check(backend.Events.TakeLast(2).SequenceEqual(["show:2000001", "hide:2000001"]), "UI hide or disposal closes an open tooltip without duplicate cleanup");
        backend.Ready = false;
        hover.BeginFrame(); hover.Request(normal); hover.EndFrame(); hover.BeginFrame(); hover.EndFrame();
        Check(backend.Events.Last() == "show:100", "unavailable native addon is never recorded as owned or closed");
        var nativeState = new ManagerBackend(); var sequence = new TooltipHover(nativeState);
        sequence.BeginFrame(); sequence.Request(normal); sequence.EndFrame();
        sequence.BeginFrame(); sequence.Request(hq); sequence.EndFrame();
        sequence.BeginFrame(); sequence.Request(key); sequence.EndFrame();
        Check(nativeState.Opened.SequenceEqual([normal, hq, key]) && nativeState.CachedHoveredItem == key.RawId,
            "second and third items open despite stale hover cache and residual tooltip type after closure");
        sequence.Close();
        sequence.BeginFrame(); sequence.Request(normal); sequence.EndFrame();
        Check(nativeState.Opened.Count == 4, "leaving links and returning opens another tooltip with uncleared hover cache");
        nativeState.DelayClose = true;
        sequence.BeginFrame(); sequence.Request(hq); sequence.EndFrame();
        Check(nativeState.Opened.Count == 4, "transition waits while native item agent is still closing");
        nativeState.AgentActive = false;
        sequence.BeginFrame(); sequence.Request(hq); sequence.EndFrame();
        Check(nativeState.Opened.Last() == hq && nativeState.Opened.Count == 5,
            "pending hover retries after native closure completes");
        nativeState.TakeOver();
        sequence.BeginFrame(); sequence.Request(hq); sequence.EndFrame();
        Check(nativeState.AgentActive && nativeState.Opened.Count == 5,
            "ownership loss during unchanged hover preserves foreign tooltip and waits");
        nativeState.ReleaseForeign();
        sequence.BeginFrame(); sequence.Request(hq); sequence.EndFrame();
        Check(nativeState.Opened.Count == 6 && nativeState.Opened.Last() == hq,
            "unchanged hover reopens after foreign tooltip releases ownership");
        Check(!NativeItemTooltip.CanShow(false, true, false)
            && !NativeItemTooltip.CanShow(false, false, true)
            && NativeItemTooltip.CanShow(true, true, true),
            "foreign tooltip target and unowned active item agent prevent takeover while owned target remains usable");
        var chats = new WindowRect[] { new(50, 50, 500, 400) };
        var pos = TooltipPlacement.Position(new(100, 100, 200, 200), chats, new(1920, 1080));
        Check(pos == new Vector2(100, 455), "native tooltip overlapping chat is placed at nearest available side within screen");
        Check(TooltipPlacement.Position(new(800, 100, 200, 200), chats, new(1920, 1080)) == null
            && TooltipPlacement.Position(new(100, 100, 200, 200), chats, new(100, 100)) == null,
            "native tooltip outside chat or too large for screen keeps native placement");
        var hqArgs = NativeItemTooltip.Arguments(hq);
        var keyArgs = NativeItemTooltip.Arguments(key);
        Check(hqArgs.ItemArgs.ItemId == 1000100 && hqArgs.ItemArgs.Kind == FFXIVClientStructs.FFXIV.Client.Enums.DetailKind.Item
            && keyArgs.ItemArgs.ItemId == 2000001 && keyArgs.ItemArgs.Kind == FFXIVClientStructs.FFXIV.Client.Enums.DetailKind.KeyItem
            && hqArgs.ItemArgs.BuyQuantity == -1 && hqArgs.ItemArgs.Flag1 == 0,
            "typed tooltip-manager arguments preserve HQ and key-item identity without agent mutation");
        Check(typeof(AgentItemDetail).Module.ModuleVersionId == new Guid("7fda2a82-4ca8-49d0-9687-4c8eac011509")
            && Marshal.SizeOf<FFXIVClientStructs.FFXIV.Component.GUI.AtkTooltipManager.AtkTooltipArgs>() == 0x18,
            "tooltip-manager adapter matches installed module and argument structure");
        using var module = Mono.Cecil.ModuleDefinition.ReadModule(typeof(NativeItemTooltip).Assembly.Location);
        var nativeType = module.Types.Single(t => t.FullName == "PluginChatOverlay.NativeItemTooltip");
        var calls = nativeType.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions)
            .Select(i => i.Operand).OfType<Mono.Cecil.MethodReference>().ToArray();
        Check(calls.Any(m => m.DeclaringType.Name == "AtkTooltipManager" && m.Name == "ShowTooltip")
            && calls.Any(m => m.DeclaringType.Name == "AtkTooltipManager" && m.Name == "HideTooltip")
            && !calls.Any(m => m.DeclaringType.Name == "AgentItemDetail" && m.Name == "ReceiveEvent")
            && !calls.Any(m => m.DeclaringType.Name == "AtkUnitBase" && m.Name is "Show" or "Hide"),
            "native tooltip uses manager protocol and excludes prior addon/agent procedure");
        Check(!calls.Any(m => m.Name == "get_HoveredItem"),
            "compiled native tooltip never uses event-cached hover ID as a display or placement gate");
    }
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    private sealed class Backend : IItemTooltipBackend
    {
        public readonly List<string> Events = [];
        public bool Ready = true;
        private TooltipItem? current;
        public bool IsOpen(TooltipItem item) => current == item;
        public bool Show(TooltipItem item) { Events.Add("show:" + item.RawId); if (Ready) current = item; return Ready; }
        public void Hide(TooltipItem item) { Events.Add("hide:" + item.RawId); current = null; }
    }
    private sealed class ManagerBackend : IItemTooltipBackend
    {
        public readonly List<TooltipItem> Opened = [];
        public uint CachedHoveredItem;
        public bool AgentActive, DelayClose;
        private bool ownsTarget, hasTarget, tooltipType;
        public bool IsOpen(TooltipItem item) => ownsTarget && AgentActive && CachedHoveredItem == item.RawId;
        public void TakeOver() { ownsTarget = false; hasTarget = tooltipType = AgentActive = true; }
        public void ReleaseForeign() { hasTarget = AgentActive = false; }
        public bool Show(TooltipItem item)
        {
            if (!NativeItemTooltip.CanShow(ownsTarget, hasTarget && tooltipType, AgentActive)) return false;
            ownsTarget = hasTarget = tooltipType = AgentActive = true;
            CachedHoveredItem = item.RawId;
            Opened.Add(item); return true;
        }
        public void Hide(TooltipItem item)
        {
            if (!ownsTarget) return;
            ownsTarget = hasTarget = false;
            AgentActive = DelayClose;
            // Simulate a native manager release with no Dalamud item-out event:
            // the ID and type flag remain set, but no target owns the tooltip.
        }
    }
}
