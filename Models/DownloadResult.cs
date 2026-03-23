namespace GameCheatsManager.WinUI.Models;

public sealed class DownloadResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? InstructionDirectory { get; init; }
}
