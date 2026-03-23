using System.Diagnostics;
using GameCheatsManager.WinUI.Interop;

namespace GameCheatsManager.WinUI.Services;

public sealed class ProcessService
{
    public sealed record ShellLaunchResult(bool Started, int ErrorCode, string Message);

    public void OpenWithShell(string target)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true
        });
    }

    public void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    public ShellLaunchResult ShellLaunch(string path, bool runAsAdministrator, string? workingDirectory = null, nint ownerWindow = default)
    {
        var result = Win32Native.ShellExecute(
            ownerWindow,
            runAsAdministrator ? "runas" : "open",
            path,
            null,
            workingDirectory,
            1);

        var errorCode = result.ToInt64() > int.MaxValue ? -1 : (int)result.ToInt64();
        return errorCode > 32
            ? new ShellLaunchResult(true, 0, string.Empty)
            : new ShellLaunchResult(false, errorCode, GetShellLaunchErrorMessage(errorCode, runAsAdministrator));
    }

    public async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        string? workingDirectory = null,
        bool createNoWindow = true)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory ?? string.Empty,
                UseShellExecute = false,
                CreateNoWindow = createNoWindow,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdoutTask, await stderrTask);
    }

    private static string GetShellLaunchErrorMessage(int errorCode, bool runAsAdministrator) =>
        errorCode switch
        {
            0 => "The operating system ran out of memory or resources while starting the trainer.",
            2 => "The trainer file could not be found.",
            3 => "The trainer path could not be found.",
            5 when runAsAdministrator => "Trainer launch was denied or canceled by UAC.",
            5 => "Access was denied when starting the trainer.",
            8 => "The system ran out of memory while starting the trainer.",
            26 => "A sharing violation prevented the trainer from starting.",
            27 => "The file association is incomplete or invalid.",
            28 => "The DDE operation timed out while starting the trainer.",
            29 => "The DDE operation failed while starting the trainer.",
            30 => "The file association is busy.",
            31 => "No application is associated with this trainer file type.",
            _ when runAsAdministrator => "Failed to start the trainer with administrator privileges.",
            _ => "Failed to start the trainer."
        };
}
