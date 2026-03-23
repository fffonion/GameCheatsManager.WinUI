using System.Globalization;
using System.Text.Json;
using GameCheatsManager.WinUI.Models;

namespace GameCheatsManager.WinUI.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public async Task<AppSettings> LoadAsync()
    {
        Directory.CreateDirectory(AppPaths.SettingsDirectory);
        Directory.CreateDirectory(AppPaths.DatabaseDirectory);

        var defaults = CreateDefaults();
        if (!File.Exists(AppPaths.SettingsFile))
        {
            Directory.CreateDirectory(defaults.DownloadPath);
            await SaveAsync(defaults);
            return defaults;
        }

        try
        {
            await using var stream = File.OpenRead(AppPaths.SettingsFile);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions) ?? defaults;
            settings = MergeWithDefaults(settings, defaults);
            Directory.CreateDirectory(settings.DownloadPath);
            await SaveAsync(settings);
            return settings;
        }
        catch
        {
            Directory.CreateDirectory(defaults.DownloadPath);
            await SaveAsync(defaults);
            return defaults;
        }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        Directory.CreateDirectory(AppPaths.SettingsDirectory);
        await using var stream = File.Create(AppPaths.SettingsFile);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
    }

    private static AppSettings CreateDefaults() =>
        new()
        {
            DownloadPath = AppPaths.GetDefaultDownloadPath(),
            Language = GetDefaultLanguage(),
            Theme = "dark",
            SafePath = true,
            EnSearchResults = false,
            SortByOrigin = true,
            CheckAppUpdate = true,
            LaunchAppOnStartup = false,
            ShowWarning = true,
            ShowCEPrompt = true,
            AutoUpdateTranslations = true,
            LastSeenAnnouncementId = string.Empty,
            EnableGCM = true,
            AutoUpdateGCMData = true,
            AutoUpdateGCMTrainers = true,
            FlingDownloadServer = "gcm",
            RemoveFlingBgMusic = true,
            AutoUpdateFlingData = true,
            AutoUpdateFlingTrainers = true,
            EnableXiaoXing = true,
            UnlockXiaoXing = false,
            AutoUpdateXiaoXingData = true,
            AutoUpdateXiaoXingTrainers = true,
            WeModPath = AppPaths.GetDefaultWeModPath(),
            CePath = AppPaths.GetDefaultCheatEnginePath(),
            EnableCT = true,
            AutoUpdateCTData = true,
            AutoUpdateCTTrainers = true,
            CevoPath = string.Empty
        };

    private static AppSettings MergeWithDefaults(AppSettings current, AppSettings defaults)
    {
        current.DownloadPath = string.IsNullOrWhiteSpace(current.DownloadPath) ? defaults.DownloadPath : current.DownloadPath;
        current.Language = string.IsNullOrWhiteSpace(current.Language) ? defaults.Language : current.Language;
        current.Theme = current.Theme is "dark" or "light" ? current.Theme : defaults.Theme;
        current.FlingDownloadServer = current.FlingDownloadServer is "official" or "gcm" ? current.FlingDownloadServer : defaults.FlingDownloadServer;
        current.WeModPath = string.IsNullOrWhiteSpace(current.WeModPath) ? defaults.WeModPath : current.WeModPath;
        current.CePath = string.IsNullOrWhiteSpace(current.CePath) ? defaults.CePath : current.CePath;
        current.CevoPath ??= string.Empty;
        return current;
    }

    private static string GetDefaultLanguage()
    {
        var culture = CultureInfo.InstalledUICulture.Name;
        return culture switch
        {
            "zh-CN" => "zh_CN",
            "zh-SG" => "zh_CN",
            "zh-Hans" => "zh_CN",
            "zh-TW" => "zh_TW",
            "zh-HK" => "zh_TW",
            "zh-MO" => "zh_TW",
            "de-DE" => "de_DE",
            "de-AT" => "de_DE",
            "de-BE" => "de_DE",
            "de-CH" => "de_DE",
            "de-LI" => "de_DE",
            "de-LU" => "de_DE",
            _ => "en_US"
        };
    }
}
