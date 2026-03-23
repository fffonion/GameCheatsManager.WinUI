using System.Runtime.InteropServices;

namespace GameCheatsManager.WinUI.Interop;

internal static class Win32Native
{
    [DllImport("kernel32.dll", EntryPoint = "GetShortPathNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern uint GetShortPathName(string longPath, System.Text.StringBuilder? shortPath, uint bufferLength);

    [DllImport("shell32.dll", EntryPoint = "ShellExecuteW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint ShellExecute(
        nint hwnd,
        string operation,
        string file,
        string? parameters,
        string? directory,
        int showCommand);
}
