using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;

namespace PluginChatOverlay;

public sealed partial class Plugin
{
    private void CompleteCopyRequest()
    {
        if (!copyInput.TakeRequest(out var requestedView)) return;
        if (!chatFocused || requestedView is not ViewState copyView || !ReferenceEquals(copyView, focusedView)) return;
        CopySelection(copyView, copyView.Documents);
        foreach (var view in views.Values) view.Selection.Clear();
        // Run after every End and pass an actual null window, without string marshalling.
        ImGuiP.ClearActiveID();
        ImGuiP.FocusWindow(default(ImGuiWindowPtr));
        chatFocused = false; focusedView = null;
    }
    private void SuppressGameCopy(IFramework _)
    {
        if (disposed) return;
        PruneHistoryCaches();
        if (Environment.TickCount64 - Interlocked.Read(ref inputUpdatedAt) > 250)
        {
            copyInput.Suppress(false, keyState[0x11], keyState[0x43]);
            copyInput.CancelRequest(); return;
        }
        // Public Update is invoked before the game's original Framework tick in this fixed build.
        if (copyInput.Suppress(chatFocused, controlDown || keyState[0x11], copyKeyDown || keyState[0x43], focusedView)) keyState[0x43] = false;
    }
    private static void CopySelection(ViewState state, DisplayDocument[] docs)
    {
        var text = state.Selection.Extract(docs.Select(d => (d.Record.Id, d.Text)).ToArray());
        if (text.Length > 0) ImGui.SetClipboardText(text);
    }
}
