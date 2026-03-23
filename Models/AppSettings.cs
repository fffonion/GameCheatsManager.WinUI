namespace GameCheatsManager.WinUI.Models;

public sealed class AppSettings
{
    public string DownloadPath { get; set; } = string.Empty;

    public string Language { get; set; } = "en_US";

    public string Theme { get; set; } = "dark";

    public bool SafePath { get; set; } = true;

    public bool EnSearchResults { get; set; }

    public bool SortByOrigin { get; set; } = true;

    public bool CheckAppUpdate { get; set; } = true;

    public bool LaunchAppOnStartup { get; set; }

    public bool ShowWarning { get; set; } = true;

    public bool ShowCEPrompt { get; set; } = true;

    public bool AutoUpdateTranslations { get; set; } = true;

    public string LastSeenAnnouncementId { get; set; } = string.Empty;

    public bool EnableGCM { get; set; } = true;

    public bool AutoUpdateGCMData { get; set; } = true;

    public bool AutoUpdateGCMTrainers { get; set; } = true;

    public string FlingDownloadServer { get; set; } = "gcm";

    public bool RemoveFlingBgMusic { get; set; } = true;

    public bool AutoUpdateFlingData { get; set; } = true;

    public bool AutoUpdateFlingTrainers { get; set; } = true;

    public bool EnableXiaoXing { get; set; } = true;

    public bool UnlockXiaoXing { get; set; }

    public bool AutoUpdateXiaoXingData { get; set; } = true;

    public bool AutoUpdateXiaoXingTrainers { get; set; } = true;

    public string WeModPath { get; set; } = string.Empty;

    public string CePath { get; set; } = string.Empty;

    public bool EnableCT { get; set; } = true;

    public bool AutoUpdateCTData { get; set; } = true;

    public bool AutoUpdateCTTrainers { get; set; } = true;

    public string CevoPath { get; set; } = string.Empty;
}
