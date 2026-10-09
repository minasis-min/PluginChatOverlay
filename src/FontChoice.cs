using Dalamud.Interface.FontIdentifier;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PluginChatOverlay;

internal static class FontChoice
{
    // Only known public identifiers may be restored; never deserialize arbitrary CLR types.
    public static IFontId? Restore(string type, string json) => type switch
    {
        nameof(SystemFontId) => JsonConvert.DeserializeObject<SystemFontId>(json, new FamilyConverter()),
        nameof(GameFontAndFamilyId) => JsonConvert.DeserializeObject<GameFontAndFamilyId>(json),
        nameof(DalamudAssetFontAndFamilyId) => JsonConvert.DeserializeObject<DalamudAssetFontAndFamilyId>(json),
        nameof(DalamudDefaultFontAndFamilyId) => DalamudDefaultFontAndFamilyId.Instance,
        "" => null,
        _ => throw new FormatException("Unsupported font identifier"),
    };
    public static void Store(Settings settings, IFontId id)
    {
        var type = id.GetType().Name;
        var json = JsonConvert.SerializeObject(id);
        _ = Restore(type, json);
        settings.FontIdType = type; settings.FontIdJson = json;
    }
    private sealed class FamilyConverter : JsonConverter
    {
        public override bool CanConvert(Type type) => type == typeof(IFontFamilyId);
        public override bool CanWrite => false;
        public override object? ReadJson(JsonReader reader, Type type, object? existing, JsonSerializer serializer)
            => JObject.Load(reader).ToObject<SystemFontFamilyId>(serializer);
        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) => throw new NotSupportedException();
    }
}
