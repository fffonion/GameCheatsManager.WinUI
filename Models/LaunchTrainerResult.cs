namespace GameCheatsManager.WinUI.Models;

public sealed class LaunchTrainerResult
{
    public bool Started { get; init; }

    public bool RequiresCheatEnginePrompt { get; init; }

    public string Message { get; init; } = string.Empty;
}
