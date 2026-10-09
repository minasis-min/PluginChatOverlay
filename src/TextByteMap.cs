using System.Globalization;
using System.Text;
using Dalamud.Game.Text.SeStringHandling;
using Lumina.Text.ReadOnly;

namespace PluginChatOverlay;

internal readonly record struct TextCharacter(int Start, int Length);
internal static class TextByteMap
{
    public static Dictionary<int, TextCharacter> Build(byte[] bytes)
    {
        var plain = SeString.Parse(bytes).TextValue;
        var starts = StringInfo.ParseCombiningCharacters(plain);
        var characters = new TextCharacter[plain.Length];
        for (var i = 0; i < starts.Length; i++)
        {
            var end = i + 1 < starts.Length ? starts[i + 1] : plain.Length;
            for (var j = starts[i]; j < end; j++) characters[j] = new(starts[i], end - starts[i]);
        }
        var result = new Dictionary<int, TextCharacter>(); var textPosition = 0;
        foreach (var entry in new ReadOnlySeStringSpan(bytes).GetOffsetEnumerator())
        {
            var payload = entry.Payload;
            if (payload.Type == ReadOnlySePayloadType.Text)
            {
                var text = Encoding.UTF8.GetString(payload.Body); var byteOffset = entry.Offset; var character = 0;
                foreach (var rune in text.EnumerateRunes())
                {
                    if (textPosition + character < characters.Length) result[byteOffset] = characters[textPosition + character];
                    character += rune.Utf16SequenceLength; byteOffset += rune.Utf8SequenceLength;
                }
                textPosition += text.Length;
            }
            else
            {
                var copy = new byte[payload.EnvelopeByteLength]; payload.WriteEnvelopeTo(copy);
                var text = SeString.Parse(copy).TextValue;
                if (text.Length > 0 && textPosition < characters.Length) result[entry.Offset] = characters[textPosition];
                textPosition += text.Length;
            }
        }
        return result;
    }
}
