using Dalamud.Game.Text.Evaluator;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

namespace PluginChatOverlay;

/// <summary>Use the game's channel envelope and the host's managed evaluator, preserving SeString links.</summary>
internal sealed class GameLogFormatter(IDataManager data, ISeStringEvaluator evaluator)
{
    private readonly Dictionary<int, byte[]> formats = [];
    public byte[] Format(int kind, byte[] sender, byte[] body)
    {
        if (!formats.TryGetValue(kind, out var format))
            formats[kind] = format = data.GetExcelSheet<LogKind>().GetRow((uint)kind).Format.AsSpan().Data.ToArray();
        if (format.Length == 0) return body;
        return evaluator.Evaluate(new ReadOnlySeString(format),
            [new SeStringParameter(new ReadOnlySeString(sender)), new SeStringParameter(new ReadOnlySeString(body))]).AsSpan().Data.ToArray();
    }
}
