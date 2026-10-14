using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace PluginChatOverlay;

internal static class NativeTooltipCompatibility
{
    private const string Expected = "A07D45226A59CE9C51CE41AC3D9DDE74D6C3EC1496684A802F084A4464D5E0C3";
    private static readonly (string Type, string[] Fields, string[] Methods)[] Contracts =
    [
        ("FFXIV.Client.UI.Agent.AgentItemDetail", ["ItemId", "DetailKind", "AddonId"], ["Instance", "IsAgentActive"]),
        ("FFXIV.Client.UI.Agent.AgentInterface", [], ["IsAgentActive"]),
        ("FFXIV.Component.GUI.AtkStage", ["TooltipManager"], ["Instance"]),
        ("FFXIV.Component.GUI.AtkTooltipManager", ["TargetNode", "ParentAddonId", "TooltipType"], ["AttachTooltip", "DetachTooltip", "ShowTooltip", "HideTooltip"]),
        ("FFXIV.Component.GUI.AtkTooltipManager+AtkTooltipArgs", ["ItemArgs"], []),
        ("FFXIV.Component.GUI.AtkTooltipManager+AtkTooltipArgs+AtkTooltipItemArgs", ["ItemId", "Kind", "BuyQuantity", "Flag1"], []),
        ("FFXIV.Component.GUI.AtkResNode", ["ScreenX", "ScreenY", "ScaleX"], ["Ctor", "SetPositionFloat", "GetScaleY", "GetWidth", "GetHeight", "SetWidth", "SetHeight", "Destroy"]),
        ("FFXIV.Component.GUI.AtkUnitBase", ["Id", "WindowNode"], ["get_IsVisible", "SetPosition"]),
        ("FFXIV.Component.GUI.AtkComponentNode", ["AtkResNode"], []),
        ("FFXIV.Client.System.Memory.IMemorySpace", [], ["GetUISpace"]),
    ];
    internal static string Diagnostic { get; private set; } = "未確認";
    internal static bool Compatible { get; } = Check();
    private static bool Check()
    {
        try
        {
            var parts = new Dictionary<string, string>();
            var fingerprint = Fingerprint(observe: (key, value) => parts[key] = value);
            var compatible = Matches(fingerprint);
            Diagnostic = compatible ? "互換性あり" : NativeTooltipContractDetails.Explain(parts);
            return compatible;
        }
        catch (Exception ex)
        {
            var detail = ex switch
            {
                TypeLoadException type => type.TypeName,
                MissingMemberException or NotSupportedException => ex.Message,
                _ => ex.GetType().Name,
            };
            Diagnostic = "非互換／確認失敗: " + detail; return false;
        }
    }
    internal static bool Matches(string fingerprint) => fingerprint == Expected;
    internal static string Fingerprint(Func<string, Type>? resolve = null, Action<string, string>? observe = null)
    {
        var assembly = typeof(AgentItemDetail).Assembly;
        var text = new StringBuilder();
        foreach (var contract in Contracts)
        {
            var type = resolve?.Invoke("FFXIVClientStructs." + contract.Type) ?? assembly.GetType("FFXIVClientStructs." + contract.Type, true)!;
            var layout = type.StructLayoutAttribute;
            if (layout is not { Value: LayoutKind.Explicit, Size: > 0 }) throw new NotSupportedException(type.FullName + ": 明示的なネイティブ配置がありません。");
            text.Append('|').Append(type.FullName).Append(':').Append(layout.Pack);
            // Only allocated values need their entire size pinned. An unrelated new field
            // in a game-owned stage/agent may grow its size without moving the fields we use.
            if (contract.Type is "FFXIV.Component.GUI.AtkResNode" or "FFXIV.Component.GUI.AtkTooltipManager+AtkTooltipArgs"
                or "FFXIV.Component.GUI.AtkTooltipManager+AtkTooltipArgs+AtkTooltipItemArgs") text.Append(':').Append(layout.Size);
            observe?.Invoke("layout:" + contract.Type, layout.Pack + (contract.Type is "FFXIV.Component.GUI.AtkResNode"
                or "FFXIV.Component.GUI.AtkTooltipManager+AtkTooltipArgs" or "FFXIV.Component.GUI.AtkTooltipManager+AtkTooltipArgs+AtkTooltipItemArgs" ? ":" + layout.Size : ""));
            foreach (var name in contract.Fields)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public) ?? throw new MissingFieldException(type.FullName, name);
                var offset = field.GetCustomAttribute<FieldOffsetAttribute>() ?? throw new NotSupportedException(type.FullName + "." + name + ": 項目位置が取得できません。");
                text.Append('|').Append(name).Append(':').Append(offset.Value).Append(':').Append(field.FieldType.FullName);
                observe?.Invoke("field:" + contract.Type + "." + name, offset.Value + ":" + field.FieldType.FullName);
            }
            foreach (var name in contract.Methods)
            {
                var count = name switch { "ShowTooltip" => 7, "AttachTooltip" => 4, "HideTooltip" => 2,
                    "DetachTooltip" or "Destroy" or "SetWidth" or "SetHeight" => 1,
                    "SetPosition" or "SetPositionFloat" => 2, _ => 0 };
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(m => m.Name == name && m.GetParameters().Length == count).ToArray();
                if (methods.Length != 1) throw new MissingMethodException(type.FullName, name);
                foreach (var method in methods)
                {
                    var signature = MethodCompatibility.Member(method); text.Append('|').Append(signature);
                    observe?.Invoke("method:" + contract.Type + "." + name, signature);
                }
            }
        }
        foreach (var pair in new[] { ("FFXIV.Client.Enums.DetailKind", "Item"), ("FFXIV.Client.Enums.DetailKind", "KeyItem"),
                     ("FFXIV.Component.GUI.AtkTooltipType", "Item") })
        {
            var type = assembly.GetType("FFXIVClientStructs." + pair.Item1, true)!;
            text.Append('|').Append(pair).Append(':').Append(type.GetField(pair.Item2)!.GetRawConstantValue());
            observe?.Invoke("enum:" + pair.Item1 + "." + pair.Item2, type.GetField(pair.Item2)!.GetRawConstantValue()!.ToString()!);
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}
