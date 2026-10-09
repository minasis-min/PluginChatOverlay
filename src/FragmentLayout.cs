using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Dalamud.Interface.ImGuiSeStringRenderer;
using Dalamud.Interface.Utility;
using Lumina.Text;
using Lumina.Text.Payloads;
using Lumina.Text.ReadOnly;

namespace PluginChatOverlay;

internal readonly record struct GlyphBox(int Character, int Length, Vector2 Position, Vector2 Size);

/// <summary>Copy managed fragment geometry immediately; never retain renderer state or native pointers.</summary>
internal sealed class FragmentLayout
{
    private object? renderer;
    private FieldInfo? fragments;
    private Dictionary<string, PropertyInfo>? properties;
    private bool failed;
    public string Status { get; private set; } = "";
    public unsafe GlyphBox[] Copy(byte[] bytes, float fontSize, float lineHeight)
    {
        if (failed) return [];
        try
        {
            Initialize();
            // Copy all fragment values first; icon measurements below also use the shared renderer.
            var copied = ((IEnumerable)fragments!.GetValue(renderer)!).Cast<object>().Select(fragment => new Fragment(
                (int)properties!["From"].GetValue(fragment)!, (int)properties["To"].GetValue(fragment)!,
                (Vector2)properties["Offset"].GetValue(fragment)!, (Rune)properties["LastRune"].GetValue(fragment)!,
                (bool)properties["IsSoftHyphenVisible"].GetValue(fragment)!)).ToArray();
            var map = TextByteMap.Build(bytes);
            var result = new List<GlyphBox>(); var font = ImGui.GetFont(); var scale = fontSize / font.FontSize;
            var previous = default(Rune);
            foreach (var fragment in copied)
            {
                var x = 0f; var lastRune = previous;
                foreach (var unit in UtfEnumerator.From(bytes.AsSpan(fragment.From, fragment.To - fragment.From), UtfEnumeratorFlags.Utf8SeString))
                {
                    var byteOffset = fragment.From + unit.ByteOffset;
                    if (unit.IsSeStringPayload && unit.MacroCode is MacroCode.Icon or MacroCode.Icon2)
                    {
                        var payload = new ReadOnlySeStringSpan(bytes.AsSpan(byteOffset)).GetEnumerator(); payload.MoveNext();
                        var style = new SeStringDrawParams { TargetDrawList = default(ImDrawListPtr), ScreenOffset = Vector2.Zero, Font = font, FontSize = fontSize, WrapWidth = float.MaxValue };
                        var size = ImGuiHelpers.SeStringWrapped(bytes.AsSpan(byteOffset, payload.Current.EnvelopeByteLength), style).Size;
                        x += MathF.Round(size.X); lastRune = default; continue;
                    }
                    var rune = unit.EffectiveRune;
                    if (rune.Value is 0 or 0xFFFF or 0xFFFC or '\r' or '\n' or 0x85 or 0x2028 or 0x2029) continue;
                    if (rune.Value == 0xAD) { if (!fragment.SoftHyphen) continue; rune = new('-'); }
                    var pointer = rune.Value < 0xFFFF ? font.FindGlyph((ushort)rune.Value) : font.FallbackGlyph.Handle;
                    if (pointer == null) continue;
                    var glyph = *(ImGuiHelpers.ImFontGlyphReal*)pointer;
                    if (rune.Value != glyph.Codepoint) rune = Rune.TryCreate(glyph.Codepoint, out var replacement) ? replacement : Rune.ReplacementChar;
                    var distance = lastRune.Value is > 0 and < 0xFFFF && rune.Value is > 0 and < 0xFFFF
                        ? MathF.Round(font.GetDistanceAdjustmentForPair((ushort)lastRune.Value, (ushort)rune.Value) * scale) : 0;
                    var advance = MathF.Round(glyph.AdvanceX * scale);
                    if (map.TryGetValue(byteOffset, out var character))
                        result.Add(new(character.Start, character.Length, fragment.Offset + new Vector2(x + distance, 0), new(Math.Max(1, advance), lineHeight)));
                    x += distance + advance; lastRune = rune;
                }
                previous = fragment.LastRune;
            }
            // All glyphs in a surrogate/combining sequence share one selection unit.
            return result.GroupBy(g => (g.Character, g.Length, g.Position.Y)).Select(group =>
            {
                var left = group.Min(g => g.Position.X); var right = group.Max(g => g.Position.X + g.Size.X);
                return new GlyphBox(group.Key.Character, group.Key.Length, new(left, group.Key.Y), new(right - left, fontSize));
            }).ToArray();
        }
        catch (Exception ex) { failed = true; Status = "文章選択の配置情報を取得できません: " + ex.Message; return []; }
    }
    private void Initialize()
    {
        if (renderer != null) return;
        var assembly = typeof(IChatGui).Assembly;
        if (assembly.ManifestModule.ModuleVersionId != new Guid("9a6f3e4f-c2f0-46f4-b211-7013dde20a8b"))
            throw new NotSupportedException("このDalamud版は文章選択の内部配置読取に未対応です。");
        var type = assembly.GetType("Dalamud.Interface.ImGuiSeStringRenderer.Internal.SeStringRenderer", true)!;
        var service = assembly.GetType("Dalamud.Service`1", true)!.MakeGenericType(type);
        renderer = service.GetMethod("Get", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null)!;
        fragments = type.GetField("fragmentsMainThread", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var fragmentType = assembly.GetType("Dalamud.Interface.ImGuiSeStringRenderer.Internal.TextFragment", true)!;
        properties = new[] { "From", "To", "Offset", "LastRune", "IsSoftHyphenVisible" }.ToDictionary(n => n, n => fragmentType.GetProperty(n)!);
    }
    private readonly record struct Fragment(int From, int To, Vector2 Offset, Rune LastRune, bool SoftHyphen);
    internal static int Caret(GlyphBox[] glyphs, Vector2 point, int textLength)
    {
        if (glyphs.Length == 0) return point.X <= 0 ? 0 : textLength;
        GlyphBox nearest = default; var distance = float.MaxValue;
        foreach (var glyph in glyphs)
        {
            var dx = Math.Max(glyph.Position.X - point.X, Math.Max(0, point.X - glyph.Position.X - glyph.Size.X));
            var dy = Math.Max(glyph.Position.Y - point.Y, Math.Max(0, point.Y - glyph.Position.Y - glyph.Size.Y));
            var score = dy * dy * 100 + dx * dx;
            if (score < distance) { distance = score; nearest = glyph; }
        }
        return Math.Clamp(point.X < nearest.Position.X + nearest.Size.X / 2 ? nearest.Character : nearest.Character + nearest.Length, 0, textLength);
    }
}
