namespace GameCheatsManager.WinUI.Models;

public sealed class InstalledTrainer
{
    public string DisplayName { get; set; } = string.Empty;

    public string LaunchPath { get; set; } = string.Empty;

    public string RootPath { get; set; } = string.Empty;

    public string Origin { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Extension { get; set; } = string.Empty;

    public string GcmUrl { get; set; } = string.Empty;

    public bool IsDirectoryOnly { get; set; }
}
