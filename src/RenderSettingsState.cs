namespace PluginChatOverlay;

internal readonly record struct RenderSettingsState(bool ShowTime, bool PadTimestampHour, uint TimestampColor, bool TextOutline, float OutlineStrength)
{
    internal static RenderSettingsState Capture(Settings settings) => new(settings.ShowTime, settings.PadTimestampHour, settings.TimestampColor, settings.TextOutline, settings.OutlineStrength);
}

public sealed partial class Plugin
{
    private void UpdateRenderSettings(RenderSettingsState before)
    {
        var after = RenderSettingsState.Capture(settings);
        if (before.ShowTime != after.ShowTime || before.PadTimestampHour != after.PadTimestampHour || before.TimestampColor != after.TimestampColor) presentationRevision++;
        if (before.TextOutline != after.TextOutline || before.OutlineStrength != after.OutlineStrength) InvalidateRowLayouts();
        // Font changes invalidate their owned layout in RebuildChatFont; line spacing
        // and available width are compared by each row. Other controls need no reparsing.
    }
}
