using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace PluginChatOverlay;

public sealed partial class Plugin
{
    private volatile bool chatHiddenByNativeExpansion;
    private bool nativeChatWasHidden;
    private readonly NativeChatVisibility nativeChatVisibility = new();

    private void UpdateNativeChatVisibility()
    {
        chatHiddenByNativeExpansion = settings.HideWhenNativeChatExpanded && clientState.IsLoggedIn
            && nativeChatVisibility.IsExpanded(game);
        if (chatHiddenByNativeExpansion)
        {
            chatFocused = false; focusedView = null;
            copyInput.CancelRequest();
        }
    }
}

internal sealed class NativeChatVisibility
{
    private static readonly byte[] ExpectedCodeHash = Convert.FromHexString("D9BA914720750392F49ADFF21C2A727A1C6BA7169466007644AD62F43C21F161");
    private static readonly string[] Panels = ["ChatLogPanel_0", "ChatLogPanel_1", "ChatLogPanel_2", "ChatLogPanel_3"];
    private static readonly int VisibleOffset = Marshal.OffsetOf<AtkUnitBase>(nameof(AtkUnitBase.Flags198)).ToInt32();
    private static readonly int PanelZoomOffset = Marshal.OffsetOf<AddonChatLogPanel>(nameof(AddonChatLogPanel.LogViewer)).ToInt32()
        + Marshal.OffsetOf<LogViewer>(nameof(LogViewer.IsMaximized)).ToInt32();
    private const int MainZoomOffset = 0x5E8;
    private long sampledAt;
    private bool expanded;
    public bool? Compatible { get; private set; }

    public bool IsExpanded(IGameGui game)
    {
        try
        {
            if (Compatible == null) Compatible = VerifyLayout();
            if (Compatible != true) return false;
            var now = Environment.TickCount64;
            if (now - sampledAt < 100) return expanded;
            sampledAt = now;
            expanded = ReadExpanded(game.GetAddonByName("ChatLog", 1).Address, MainZoomOffset);
            foreach (var name in Panels)
            {
                if (expanded) break;
                expanded = ReadExpanded(game.GetAddonByName(name, 1).Address, PanelZoomOffset);
            }
            return expanded;
        }
        catch
        {
            // A failed compatibility check must not repeat reflection/signature work every tick.
            Compatible ??= false;
            expanded = false; return false;
        }
    }

    private static unsafe bool VerifyLayout()
    {
        // Read, never invoke, the SDK-resolved function. Its complete machine-code
        // fingerprint pins the independently inspected main flag at 0x5E8 and
        // panel LogViewer.IsMaximized field to the supported game executable.
        Span<byte> code = stackalloc byte[181];
        return VisibleOffset == 0x198 && PanelZoomOffset == 0x3A0
            && SafeProcessMemory.Read((nint)AddonChatLog.MemberFunctionPointers.IsZoomed, code)
            && MatchesCode(code);
    }

    internal static bool MatchesCode(ReadOnlySpan<byte> code)
    {
        if (code.Length != 181) return false;
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(code, hash);
        return hash.SequenceEqual(ExpectedCodeHash);
    }

    internal static bool ReadExpanded(nint addon, int zoomOffset)
    {
        if (addon == 0) return false;
        Span<byte> visibility = stackalloc byte[4];
        Span<byte> flag = stackalloc byte[1];
        return SafeProcessMemory.Read(addon + VisibleOffset, visibility)
            && (System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(visibility) & (1u << 21)) != 0
            && SafeProcessMemory.Read(addon + zoomOffset, flag) && flag[0] == 1;
    }
}
