using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace PluginChatOverlay;

internal readonly record struct ChatPopupStyle(Vector2 Padding, float BorderWidth, float Rounding,
    Vector4 Border, Vector4 ChildBackground, Vector4 PopupBackground, Vector4 ScrollbarBackground,
    Vector4 Grab, Vector4 GrabHovered, Vector4 GrabActive, ImFontPtr Font, ChatStyleSnapshot? FullStyle = null, float Scale = 1)
{
    internal static ChatPopupStyle Capture(ChatStyleSnapshot? snapshot = null)
    {
        var style = ImGui.GetStyle();
        snapshot ??= new();
        var scale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
        snapshot.Capture(scale);
        return new(style.WindowPadding, style.WindowBorderSize, style.WindowRounding,
            style.Colors[(int)ImGuiCol.Border], style.Colors[(int)ImGuiCol.ChildBg], style.Colors[(int)ImGuiCol.PopupBg],
            style.Colors[(int)ImGuiCol.ScrollbarBg], style.Colors[(int)ImGuiCol.ScrollbarGrab],
            style.Colors[(int)ImGuiCol.ScrollbarGrabHovered], style.Colors[(int)ImGuiCol.ScrollbarGrabActive], ImGui.GetFont(), snapshot, scale);
    }

    internal Scope PushFor(string popupId) => new(this,
        ImGui.IsMouseReleased(ImGuiMouseButton.Right) || ImGui.IsPopupOpen(popupId));

    internal readonly struct Scope : IDisposable
    {
        private readonly bool active;
        private readonly ChatStyleSnapshot.Scope? fullScope;
        internal Scope(ChatPopupStyle style, bool active)
        {
            this.active = active;
            fullScope = null;
            if (!active) return;
            ImGui.PushFont(style.Font);
            if (style.FullStyle != null)
            {
                try { fullScope = style.FullStyle.Push(style.Scale); }
                catch { ImGui.PopFont(); throw; }
                return;
            }
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, style.Padding);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, style.BorderWidth);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, style.Rounding);
            ImGui.PushStyleColor(ImGuiCol.Border, style.Border);
            ImGui.PushStyleColor(ImGuiCol.ChildBg, style.ChildBackground);
            ImGui.PushStyleColor(ImGuiCol.PopupBg, style.PopupBackground);
            ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, style.ScrollbarBackground);
            ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, style.Grab);
            ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, style.GrabHovered);
            ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, style.GrabActive);
        }
        public void Dispose()
        {
            if (!active) return;
            if (fullScope is { } full) { full.Dispose(); ImGui.PopFont(); return; }
            ImGui.PopStyleColor(7);
            ImGui.PopStyleVar(3);
            ImGui.PopFont();
        }
    }
}
