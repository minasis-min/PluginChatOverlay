using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace PluginChatOverlay;

// Only values owned by our theme are captured. No fonts, pointers or host resources are retained.
internal sealed class ChatStyleSnapshot
{
    public float[] Colors { get; set; } = [];
    public float[] Metrics { get; set; } = [];
    private static readonly ImGuiStyleVar[] VectorVars = [ImGuiStyleVar.WindowPadding, ImGuiStyleVar.FramePadding,
        ImGuiStyleVar.ItemSpacing, ImGuiStyleVar.ItemInnerSpacing, ImGuiStyleVar.CellPadding];
    private static readonly ImGuiStyleVar[] ScalarVars = [ImGuiStyleVar.WindowRounding, ImGuiStyleVar.ChildRounding,
        ImGuiStyleVar.PopupRounding, ImGuiStyleVar.FrameRounding, ImGuiStyleVar.GrabRounding, ImGuiStyleVar.ScrollbarRounding,
        ImGuiStyleVar.WindowBorderSize, ImGuiStyleVar.ChildBorderSize, ImGuiStyleVar.PopupBorderSize, ImGuiStyleVar.FrameBorderSize,
        ImGuiStyleVar.ScrollbarSize, ImGuiStyleVar.GrabMinSize, ImGuiStyleVar.DisabledAlpha];
    internal bool IsValid => Colors is { Length: (int)ImGuiCol.Count * 4 } && Metrics is { Length: 23 }
        && Colors.All(v => float.IsFinite(v) && v >= 0 && v <= 1)
        && Metrics.All(v => float.IsFinite(v) && v >= 0 && v <= 200) && Metrics[22] <= 1;
    internal float MessageGap => Metrics[5];
    internal static float Scale(float scale) => float.IsFinite(scale) && scale > 0 ? scale : 1;

    internal void Capture(float scale)
    {
        scale = Scale(scale);
        var style = ImGui.GetStyle();
        if (Colors.Length != (int)ImGuiCol.Count * 4) Colors = new float[(int)ImGuiCol.Count * 4];
        if (Metrics.Length != 23) Metrics = new float[23];
        for (var i = 0; i < (int)ImGuiCol.Count; i++)
        {
            var c = style.Colors[i]; var offset = i * 4;
            Colors[offset] = c.X; Colors[offset + 1] = c.Y; Colors[offset + 2] = c.Z; Colors[offset + 3] = c.W;
        }
        ReadOnlySpan<Vector2> vectors = [style.WindowPadding, style.FramePadding, style.ItemSpacing, style.ItemInnerSpacing, style.CellPadding];
        for (var i = 0; i < vectors.Length; i++) { Metrics[i * 2] = vectors[i].X / scale; Metrics[i * 2 + 1] = vectors[i].Y / scale; }
        ReadOnlySpan<float> scalars = [style.WindowRounding, style.ChildRounding, style.PopupRounding, style.FrameRounding,
            style.GrabRounding, style.ScrollbarRounding, style.WindowBorderSize, style.ChildBorderSize,
            style.PopupBorderSize, style.FrameBorderSize, style.ScrollbarSize, style.GrabMinSize, style.DisabledAlpha];
        for (var i = 0; i < scalars.Length; i++) Metrics[10 + i] = i == 12 ? scalars[i] : scalars[i] / scale;
    }

    internal Scope Push(float scale) => new(this, Scale(scale));
    internal readonly struct Scope : IDisposable
    {
        private readonly int colors;
        private readonly int variables;
        internal Scope(ChatStyleSnapshot snapshot, float scale)
        {
            var c = 0; var v = 0;
            try
            {
                for (var i = 0; i < (int)ImGuiCol.Count; i++)
                {
                    var offset = i * 4;
                    ImGui.PushStyleColor((ImGuiCol)i, new Vector4(snapshot.Colors[offset], snapshot.Colors[offset + 1], snapshot.Colors[offset + 2], snapshot.Colors[offset + 3])); c++;
                }
                for (var i = 0; i < VectorVars.Length; i++)
                { ImGui.PushStyleVar(VectorVars[i], new Vector2(snapshot.Metrics[i * 2], snapshot.Metrics[i * 2 + 1]) * scale); v++; }
                for (var i = 0; i < ScalarVars.Length; i++)
                { ImGui.PushStyleVar(ScalarVars[i], snapshot.Metrics[10 + i] * (i == 12 ? 1 : scale)); v++; }
            }
            catch { if (v > 0) ImGui.PopStyleVar(v); if (c > 0) ImGui.PopStyleColor(c); throw; }
            colors = c; variables = v;
        }
        public void Dispose()
        { if (variables > 0) ImGui.PopStyleVar(variables); if (colors > 0) ImGui.PopStyleColor(colors); }
    }
}
