using System.Globalization;
using System.Text.Json;
using GameCheatsManager.WinUI.Models;

namespace GameCheatsManager.WinUI.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true
    };

    public async Task<AppSettings> LoadAsync()
    {
        Directory.CreateDirectory(AppPaths.SettingsDirectory);
        Directory.CreateDirectory(AppPaths.DatabaseDirectory);

        var defaults = CreateDefaults();
        var settings = await LoadStoredSettingsAsync(defaults);
        settings = MergeWithDefaults(settings, defaults);
        settings.DownloadPath = ResolveDownloadPath(settings.DownloadPath, defaults.DownloadPath);
        settings.WeModPath = NormalizeOptionalPath(settings.WeModPath);
        settings.CePath = NormalizeOptionalPath(settings.CePath);
        settings.CevoPath = NormalizeOptionalPath(settings.CevoPath);

        await SaveAsync(settings);
        return settings;
    }

    public async Task SaveAsync(AppSettings settings)
    {
        Directory.CreateDirectory(AppPaths.SettingsDirectory);
        var tempFile = $"{AppPaths.SettingsFile}.tmp";
        if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }

        await using (var stream = File.Create(tempFile))
        {
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
            await stream.FlushAsync();
        }

        if (File.Exists(AppPaths.SettingsFile))
        {
            File.Replace(tempFile, AppPaths.SettingsFile, null, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(tempFile, AppPaths.SettingsFile);
        }
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

    private static async Task<AppSettings> LoadStoredSettingsAsync(AppSettings defaults)
    {
        if (!File.Exists(AppPaths.SettingsFile))
        {
            return defaults;
        }

        try
        {
            await using var stream = File.OpenRead(AppPaths.SettingsFile);
            return await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions) ?? defaults;
        }
        catch
        {
            return defaults;
        }
    }

    private static string ResolveDownloadPath(string? savedPath, string fallbackPath)
    {
        if (TryEnsureDirectory(savedPath, out var resolvedPath))
        {
            return resolvedPath;
        }

        if (TryEnsureDirectory(fallbackPath, out resolvedPath))
        {
            return resolvedPath;
        }

        var emergencyPath = Path.Combine(Path.GetTempPath(), "GCM Trainers");
        if (TryEnsureDirectory(emergencyPath, out resolvedPath))
        {
            return resolvedPath;
        }

        return fallbackPath;
    }

    private static string NormalizeOptionalPath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? string.Empty : path;

    private static bool TryEnsureDirectory(string? path, out string normalizedPath)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            normalizedPath = Path.GetFullPath(path);
            Directory.CreateDirectory(normalizedPath);
            return true;
        }
        catch
        {
            normalizedPath = string.Empty;
            return false;
        }
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
