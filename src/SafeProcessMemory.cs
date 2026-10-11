using System.Runtime.InteropServices;

namespace PluginChatOverlay;

internal static class SafeProcessMemory
{
    // The OS validates the entire source range, including pages concurrently
    // freed by the game. Never dereference a game pointer in managed code.
    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern unsafe bool ReadProcessMemory(nint process, nint address, void* buffer, nuint size, out nuint read);

    internal static unsafe bool Read(nint address, Span<byte> destination)
    {
        if (address == 0 || destination.IsEmpty) return false;
        fixed (byte* buffer = destination)
            return ReadProcessMemory(new nint(-1), address, buffer, (nuint)destination.Length, out var count)
                && count == (nuint)destination.Length;
    }
}
