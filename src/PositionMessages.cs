using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
namespace PluginChatOverlay;
internal static class PositionMessages
{
    public static bool HasMapLink(byte[] body)
    {
        try { return SeString.Parse(body).Payloads.Any(p => p is MapLinkPayload); }
        catch { return false; }
    }
    public static bool IsPlayerKind(int kind) => (XivChatType)kind is XivChatType.Say or XivChatType.Shout or XivChatType.Yell
        or XivChatType.TellIncoming or XivChatType.TellOutgoing or XivChatType.Party or XivChatType.CrossParty
        or XivChatType.Alliance or XivChatType.FreeCompany or XivChatType.NoviceNetwork or XivChatType.PvPTeam
        or XivChatType.Ls1 or XivChatType.Ls2 or XivChatType.Ls3 or XivChatType.Ls4 or XivChatType.Ls5 or XivChatType.Ls6 or XivChatType.Ls7 or XivChatType.Ls8
        or XivChatType.CrossLinkShell1 or XivChatType.CrossLinkShell2 or XivChatType.CrossLinkShell3 or XivChatType.CrossLinkShell4
        or XivChatType.CrossLinkShell5 or XivChatType.CrossLinkShell6 or XivChatType.CrossLinkShell7 or XivChatType.CrossLinkShell8;
}
