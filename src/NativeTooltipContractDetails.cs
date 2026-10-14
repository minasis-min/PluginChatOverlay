namespace PluginChatOverlay;

internal static class NativeTooltipContractDetails
{
    // Baseline metadata only; no DLL identifiers, pointers, paths or user data.
    internal static readonly Dictionary<string, string> Expected = new()
    {
        ["enum:FFXIV.Client.Enums.DetailKind.Item"] = "1",
        ["enum:FFXIV.Client.Enums.DetailKind.KeyItem"] = "8",
        ["enum:FFXIV.Component.GUI.AtkTooltipType.Item"] = "2",
        ["field:FFXIV.Client.UI.Agent.AgentItemDetail.AddonId"] = "32:System.UInt32",
        ["field:FFXIV.Client.UI.Agent.AgentItemDetail.DetailKind"] = "280:FFXIVClientStructs.FFXIV.Client.Enums.DetailKind",
        ["field:FFXIV.Client.UI.Agent.AgentItemDetail.ItemId"] = "312:System.UInt32",
        ["field:FFXIV.Component.GUI.AtkComponentNode.AtkResNode"] = "0:FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode",
        ["field:FFXIV.Component.GUI.AtkResNode.ScaleX"] = "76:System.Single",
        ["field:FFXIV.Component.GUI.AtkResNode.ScreenX"] = "112:System.Single",
        ["field:FFXIV.Component.GUI.AtkResNode.ScreenY"] = "116:System.Single",
        ["field:FFXIV.Component.GUI.AtkStage.TooltipManager"] = "360:FFXIVClientStructs.FFXIV.Component.GUI.AtkTooltipManager",
        ["field:FFXIV.Component.GUI.AtkTooltipManager.ParentAddonId"] = "488:System.UInt16",
        ["field:FFXIV.Component.GUI.AtkTooltipManager.TargetNode"] = "32:FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode*",
        ["field:FFXIV.Component.GUI.AtkTooltipManager.TooltipType"] = "500:System.Byte",
        ["field:FFXIV.Component.GUI.AtkTooltipManager+AtkTooltipArgs.ItemArgs"] = "0:FFXIVClientStructs.FFXIV.Component.GUI.AtkTooltipManager+AtkTooltipArgs+AtkTooltipItemArgs",
        ["field:FFXIV.Component.GUI.AtkTooltipManager+AtkTooltipArgs+AtkTooltipItemArgs.BuyQuantity"] = "16:System.Int32",
        ["field:FFXIV.Component.GUI.AtkTooltipManager+AtkTooltipArgs+AtkTooltipItemArgs.Flag1"] = "12:System.UInt32",
        ["field:FFXIV.Component.GUI.AtkTooltipManager+AtkTooltipArgs+AtkTooltipItemArgs.ItemId"] = "8:System.Int32",
        ["field:FFXIV.Component.GUI.AtkTooltipManager+AtkTooltipArgs+AtkTooltipItemArgs.Kind"] = "22:FFXIVClientStructs.FFXIV.Client.Enums.DetailKind",
        ["field:FFXIV.Component.GUI.AtkUnitBase.Id"] = "484:System.UInt16",
        ["field:FFXIV.Component.GUI.AtkUnitBase.WindowNode"] = "288:FFXIVClientStructs.FFXIV.Component.GUI.AtkComponentNode*",
        ["layout:FFXIV.Client.System.Memory.IMemorySpace"] = "0",
        ["layout:FFXIV.Client.UI.Agent.AgentInterface"] = "0",
        ["layout:FFXIV.Client.UI.Agent.AgentItemDetail"] = "0",
        ["layout:FFXIV.Component.GUI.AtkComponentNode"] = "0",
        ["layout:FFXIV.Component.GUI.AtkResNode"] = "0:192",
        ["layout:FFXIV.Component.GUI.AtkStage"] = "0",
        ["layout:FFXIV.Component.GUI.AtkTooltipManager"] = "0",
        ["layout:FFXIV.Component.GUI.AtkTooltipManager+AtkTooltipArgs"] = "0:24",
        ["layout:FFXIV.Component.GUI.AtkTooltipManager+AtkTooltipArgs+AtkTooltipItemArgs"] = "0:24",
        ["layout:FFXIV.Component.GUI.AtkUnitBase"] = "0",
        ["method:FFXIV.Client.System.Memory.IMemorySpace.GetUISpace"] = "static FFXIVClientStructs.FFXIV.Client.System.Memory.IMemorySpace.GetUISpace():FFXIVClientStructs.FFXIV.Client.System.Memory.IMemorySpace*",
        ["method:FFXIV.Client.UI.Agent.AgentInterface.IsAgentActive"] = "instance FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentInterface.IsAgentActive():System.Boolean",
        ["method:FFXIV.Client.UI.Agent.AgentItemDetail.Instance"] = "static FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentItemDetail.Instance():FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentItemDetail*",
        ["method:FFXIV.Client.UI.Agent.AgentItemDetail.IsAgentActive"] = "instance FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentItemDetail.IsAgentActive():System.Boolean",
        ["method:FFXIV.Component.GUI.AtkResNode.Ctor"] = "instance FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode.Ctor():FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode*",
        ["method:FFXIV.Component.GUI.AtkResNode.Destroy"] = "instance FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode.Destroy(System.Boolean):System.Void",
        ["method:FFXIV.Component.GUI.AtkResNode.GetHeight"] = "instance FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode.GetHeight():System.UInt16",
        ["method:FFXIV.Component.GUI.AtkResNode.GetScaleY"] = "instance FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode.GetScaleY():System.Single",
        ["method:FFXIV.Component.GUI.AtkResNode.GetWidth"] = "instance FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode.GetWidth():System.UInt16",
        ["method:FFXIV.Component.GUI.AtkResNode.SetHeight"] = "instance FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode.SetHeight(System.UInt16):System.Void",
        ["method:FFXIV.Component.GUI.AtkResNode.SetPositionFloat"] = "instance FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode.SetPositionFloat(System.Single,System.Single):System.Void",
        ["method:FFXIV.Component.GUI.AtkResNode.SetWidth"] = "instance FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode.SetWidth(System.UInt16):System.Void",
        ["method:FFXIV.Component.GUI.AtkStage.Instance"] = "static FFXIVClientStructs.FFXIV.Component.GUI.AtkStage.Instance():FFXIVClientStructs.FFXIV.Component.GUI.AtkStage*",
        ["method:FFXIV.Component.GUI.AtkTooltipManager.AttachTooltip"] = "instance FFXIVClientStructs.FFXIV.Component.GUI.AtkTooltipManager.AttachTooltip(FFXIVClientStructs.FFXIV.Component.GUI.AtkTooltipType,System.UInt16,FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode*,FFXIVClientStructs.FFXIV.Component.GUI.AtkTooltipManager+AtkTooltipArgs*):System.Void",
        ["method:FFXIV.Component.GUI.AtkTooltipManager.DetachTooltip"] = "instance FFXIVClientStructs.FFXIV.Component.GUI.AtkTooltipManager.DetachTooltip(FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode*):System.Void",
        ["method:FFXIV.Component.GUI.AtkTooltipManager.HideTooltip"] = "instance FFXIVClientStructs.FFXIV.Component.GUI.AtkTooltipManager.HideTooltip(System.UInt16,System.Boolean):System.Void",
        ["method:FFXIV.Component.GUI.AtkTooltipManager.ShowTooltip"] = "instance FFXIVClientStructs.FFXIV.Component.GUI.AtkTooltipManager.ShowTooltip(FFXIVClientStructs.FFXIV.Component.GUI.AtkTooltipType,System.UInt16,FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode*,FFXIVClientStructs.FFXIV.Component.GUI.AtkTooltipManager+AtkTooltipArgs*,fn[True;](System.Single*,System.Single*,FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode*):System.Void,System.Boolean,System.Boolean):System.Void",
        ["method:FFXIV.Component.GUI.AtkUnitBase.get_IsVisible"] = "instance FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase.get_IsVisible():System.Boolean",
        ["method:FFXIV.Component.GUI.AtkUnitBase.SetPosition"] = "instance FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase.SetPosition(System.Int16,System.Int16):System.Void",
    };
    internal static string Explain(IReadOnlyDictionary<string, string> actual)
    {
        var changed = Expected.Where(p => !actual.TryGetValue(p.Key, out var value) || value != p.Value).Select(p => p.Key).ToArray();
        return "非互換: " + (changed.Length == 0 ? "ツールチップの構造／APIが基準と不一致" : string.Join("、", changed));
    }
}
