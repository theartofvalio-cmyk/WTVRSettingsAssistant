using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HOTASTrimUtility;

public static class GameWindow
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    public static bool GameFocused()
    {
        try
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out uint id);
            using var process = Process.GetProcessById((int)id);
            return process.ProcessName.Equals("aces", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}
