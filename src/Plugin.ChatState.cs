using System.Numerics;

namespace PluginChatOverlay;

public sealed partial class Plugin
{
    private sealed class ViewState
    {
        public readonly Dictionary<long, RowLayout> Rows = [];
        public readonly TextSelection Selection = new();
        public bool Follow = true, Jump, ResetGeometry = true, HeaderDragging, AltDragging;
        public long ReadUpTo, AnchorId;
        public float AnchorOffset;
        public int Unread;
        public Vector2 DragMouseStart, DragWindowStart;
        public Vector2 LastDragPosition, PendingAltMouse, PendingAltWindow;
        public int PendingAltFrame = -1;
        public uint PendingAltRootId;
        public Vector2? RequestedSize;
        public string ActiveTabId = "", RequestedTabId = "";
        public double ScrollbarUntil;
        public float PreviousScroll, ScrollbarDragOffset;
        public float ScrollMaximum;
        public bool ScrollbarHovered, ScrollbarDragging;
        public float? RequestedScroll;
        public DisplayDocument[] Documents = [];
        public long[] DocumentIds = [];
        public Dictionary<long, int> DocumentIndices = [];
        public HashSet<long> ActiveIds = [];
        public DisplayDocument[]? FilterSource, UnreadDocuments;
        public DisplayDocument[] FilteredDocuments = [];
        public string[] FilterPlugins = [];
        public bool FilterAll, FilterUnknown;
        public long UnreadReadUpTo;
        public WindowRect? Window;
        public ChatWindow? Host;
    }
    private sealed class RowLayout(DisplayDocument document)
    {
        public DisplayDocument Document = document;
        public float Width, FontSize, Height = 25;
        public float MessageGap;
        public int LineSpacing = -1;
        public GlyphBox[] Glyphs = [];
    }
}
