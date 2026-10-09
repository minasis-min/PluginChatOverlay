using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.Enums;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Config;
using System.Numerics;

namespace PluginChatOverlay;

internal sealed unsafe class NativeItemTooltip(IGameGui game, IPluginLog log) : IItemTooltipBackend
{
    // Version-pinned client bindings; the standard tooltip manager owns item-detail state.
    private static readonly Guid SupportedModule = new("7fda2a82-4ca8-49d0-9687-4c8eac011509");
    private static bool Compatible => typeof(AgentItemDetail).Module.ModuleVersionId == SupportedModule;
    public string Status { get; private set; } = Compatible ? "" : "ネイティブツールチップ: FFXIVClientStructsの版が未対応です。";
    private IAddonLifecycle? lifecycle;
    private IGameConfig? config;
    private TooltipItem? shown;
    private WindowRect[] chatRects = [];
    private Vector2 screenSize, viewportOrigin;
    private AtkResNode* anchor;
    private ushort parentId;
    private bool attached;
    public void Register(IAddonLifecycle lifecycle, IGameConfig config)
    {
        this.lifecycle = lifecycle; this.config = config;
        lifecycle.RegisterListener(AddonEvent.PostUpdate, "ItemDetail", Move);
    }
    public void Unregister()
    {
        try { lifecycle?.UnregisterListener(AddonEvent.PostUpdate, "ItemDetail", Move); }
        finally
        {
            lifecycle = null; config = null; shown = null;
            if (anchor != null)
            {
                var stage = AtkStage.Instance();
                if (stage != null)
                {
                    ReleaseAnchor();
                }
                if (attached) throw new InvalidOperationException("Tooltip anchor remains registered; destruction deferred.");
                // Destroy only after successful detachment; never leave a registered dangling node.
                anchor->Destroy(true); anchor = null;
            }
        }
    }
    public void SetWindows(WindowRect[] rects, Vector2 viewportOrigin, Vector2 screenSize)
    {
        chatRects = rects.Select(r => new WindowRect(r.X - viewportOrigin.X, r.Y - viewportOrigin.Y, r.Width, r.Height)).ToArray();
        this.screenSize = screenSize; this.viewportOrigin = viewportOrigin;
        if (anchor != null) PositionAnchor();
    }
    private void Move(AddonEvent type, AddonArgs args)
    {
        try { MoveOwnedTooltip(args); }
        catch (Exception ex)
        {
            if (Status != "ネイティブツールチップの位置を調整できませんでした。") log.Warning(ex, "Native tooltip positioning failed");
            Status = "ネイティブツールチップの位置を調整できませんでした。";
        }
    }
    private void MoveOwnedTooltip(AddonArgs args)
    {
        if (!Compatible || !OwnsTooltip() || shown is not { } item || config == null
            || !config.TryGet(UiControlOption.DetailTrackingType, out uint tracking) || tracking != 0) return;
        var agent = AgentItemDetail.Instance(); var addon = (AtkUnitBase*)args.Addon.Address;
        if (agent == null || addon == null || !addon->IsVisible || addon->WindowNode == null
            || agent->ItemId != item.RawId || agent->DetailKind != Kind(item) || agent->AddonId != addon->Id) return;
        var node = addon->WindowNode->AtkResNode;
        var rect = new WindowRect(node.ScreenX, node.ScreenY, node.GetWidth() * node.ScaleX, node.GetHeight() * node.GetScaleY());
        var pos = TooltipPlacement.Position(rect, chatRects, screenSize);
        if (pos is { } location) addon->SetPosition((short)Math.Clamp(location.X, 0, short.MaxValue), (short)Math.Clamp(location.Y, 0, short.MaxValue));
    }
    private static DetailKind Kind(TooltipItem item) => item.Kind == ItemKind.EventItem ? DetailKind.KeyItem : DetailKind.Item;
    internal static AtkTooltipManager.AtkTooltipArgs Arguments(TooltipItem item) => new()
    {
        ItemArgs = new()
        {
            ItemId = checked((int)item.RawId), Kind = Kind(item), BuyQuantity = -1,
        },
    };
    private bool OwnsTooltip()
    {
        var stage = AtkStage.Instance();
        return stage != null && anchor != null && stage->TooltipManager.TargetNode == anchor
            && stage->TooltipManager.ParentAddonId == parentId;
    }
    public bool IsOpen(TooltipItem item)
    {
        if (!Compatible || shown != item || !OwnsTooltip()) return false;
        var agent = AgentItemDetail.Instance();
        return agent != null && agent->IsAgentActive() && agent->ItemId == item.RawId && agent->DetailKind == Kind(item);
    }
    // GameGui.HoveredItem is an event cache: manager-driven closure need not
    // trigger the event that clears it. TooltipType alone is not ownership either.
    internal static bool CanShow(bool ownsTarget, bool activeTarget, bool itemAgentActive)
        => ownsTarget || !activeTarget && !itemAgentActive;
    private void PositionAnchor()
    {
        var mouse = ImGui.GetMousePos() - viewportOrigin;
        anchor->SetPositionFloat(mouse.X, mouse.Y);
        anchor->ScreenX = mouse.X; anchor->ScreenY = mouse.Y;
    }
    public bool Show(TooltipItem item)
    {
        if (!Compatible || item.RawId == 0 || item.RawId > int.MaxValue) return false;
        try
        {
            if (attached && shown == null) ReleaseAnchor();
            var stage = AtkStage.Instance();
            var parent = (AtkUnitBase*)game.GetAddonByName("ChatLog", 1).Address;
            if (stage == null || parent == null) return false;
            var agent = AgentItemDetail.Instance();
            if (agent == null || !CanShow(OwnsTooltip(),
                    stage->TooltipManager.TargetNode != null && stage->TooltipManager.TooltipType != 0,
                    agent->IsAgentActive())) return false;
            // A plugin-owned anchor participates in the standard tooltip protocol.
            // No AgentItemDetail fields, addon Show/Hide or ReceiveEvent are written/called.
            if (anchor == null)
            {
                var space = IMemorySpace.GetUISpace();
                if (space == null) return false;
                anchor = space->Create<AtkResNode>();
                if (anchor == null) return false;
                anchor->SetWidth(1); anchor->SetHeight(1);
            }
            PositionAnchor(); parentId = parent->Id;
            var args = Arguments(item);
            attached = true;
            stage->TooltipManager.AttachTooltip(AtkTooltipType.Item, parentId, anchor, &args);
            stage->TooltipManager.ShowTooltip(AtkTooltipType.Item, parentId, anchor, &args);
            if (!OwnsTooltip())
            {
                ReleaseAnchor();
                return false;
            }
            shown = item; Status = ""; return true;
        }
        catch (Exception ex)
        {
            shown = null;
            Cleanup.Run(error => log.Warning(error, "Failed tooltip request cleanup failed"), ReleaseAnchor);
            Status = "ネイティブツールチップを表示できませんでした。";
            log.Warning(ex, "Native item tooltip request failed"); return false;
        }
    }
    public void Hide(TooltipItem item)
    {
        shown = null;
        if (!Compatible || anchor == null) return;
        try
        {
            ReleaseAnchor();
        }
        catch (Exception ex) { log.Warning(ex, "Native item tooltip release failed"); }
    }
    private void ReleaseAnchor()
    {
        if (anchor == null || !attached) return;
        var stage = AtkStage.Instance();
        if (stage == null) return;
        try { if (OwnsTooltip()) stage->TooltipManager.HideTooltip(parentId); }
        finally
        {
            // Detach only our node, including requests that failed after attachment.
            // Keep the flag set if detachment fails so a later attempt can retry.
            stage->TooltipManager.DetachTooltip(anchor); attached = false;
        }
    }
}
