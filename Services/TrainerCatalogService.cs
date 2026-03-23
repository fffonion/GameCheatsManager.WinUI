using System.Text.Json;
using System.Text.RegularExpressions;
using GameCheatsManager.WinUI.Models;
using HtmlAgilityPack;

namespace GameCheatsManager.WinUI.Services;

public sealed class TrainerCatalogService
{
    private static readonly Regex PrefixRegex = new(@"^\[.*?\]\s*", RegexOptions.Compiled);
    private readonly FileDownloadService _downloadService;

    public TrainerCatalogService(FileDownloadService downloadService)
    {
        _downloadService = downloadService;
    }

    public string TranslationsPath => Path.Combine(AppPaths.DatabaseDirectory, "translations.json");

    public string GcmPath => Path.Combine(AppPaths.DatabaseDirectory, "gcm_trainers.json");

    public string FlingArchiveHtmlPath => Path.Combine(AppPaths.DatabaseDirectory, "fling_archive.html");

    public string FlingArchiveJsonPath => Path.Combine(AppPaths.DatabaseDirectory, "fling_archive.json");

    public string FlingMainHtmlPath => Path.Combine(AppPaths.DatabaseDirectory, "fling_main.html");

    public string FlingMainJsonPath => Path.Combine(AppPaths.DatabaseDirectory, "fling_main.json");

    public string XiaoXingPath => Path.Combine(AppPaths.DatabaseDirectory, "xiaoxing.json");

    public string CheatTablePath => Path.Combine(AppPaths.DatabaseDirectory, "cheat_table.json");

    public async Task<bool> FetchTrainerTranslationsAsync(CancellationToken cancellationToken = default)
    {
        var signedUrl = await _downloadService.GetSignedDownloadUrlAsync("GCM/Data/translations.json", cancellationToken);
        if (string.IsNullOrWhiteSpace(signedUrl))
        {
            return false;
        }

        _ = await _downloadService.DownloadFileAsync(signedUrl, AppPaths.DatabaseDirectory, cancellationToken: cancellationToken);
        return File.Exists(TranslationsPath);
    }

    public async Task<bool> FetchGcmDataAsync(CancellationToken cancellationToken = default)
    {
        var signedUrl = await _downloadService.GetSignedDownloadUrlAsync("GCM/Data/gcm_trainers.json", cancellationToken);
        if (string.IsNullOrWhiteSpace(signedUrl))
        {
            return false;
        }

        _ = await _downloadService.DownloadFileAsync(signedUrl, AppPaths.DatabaseDirectory, cancellationToken: cancellationToken);
        return File.Exists(GcmPath);
    }

    public async Task<bool> FetchFlingDataAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        if (string.Equals(settings.FlingDownloadServer, "official", StringComparison.OrdinalIgnoreCase))
        {
            var archiveHtml = await _downloadService.GetWebPageContentAsync("https://archive.flingtrainer.com/", cancellationToken);
            await FileDownloadService.SaveTextAsync(FlingArchiveHtmlPath, archiveHtml);

            var mainHtml = await _downloadService.GetWebPageContentAsync("https://flingtrainer.com/all-trainers-a-z/", cancellationToken);
            await FileDownloadService.SaveTextAsync(FlingMainHtmlPath, mainHtml);
            return true;
        }

        var archiveUrl = await _downloadService.GetSignedDownloadUrlAsync("GCM/Data/fling_archive.json", cancellationToken);
        var mainUrl = await _downloadService.GetSignedDownloadUrlAsync("GCM/Data/fling_main.json", cancellationToken);
        if (string.IsNullOrWhiteSpace(archiveUrl) || string.IsNullOrWhiteSpace(mainUrl))
        {
            return false;
        }

        _ = await _downloadService.DownloadFileAsync(archiveUrl, AppPaths.DatabaseDirectory, cancellationToken: cancellationToken);
        _ = await _downloadService.DownloadFileAsync(mainUrl, AppPaths.DatabaseDirectory, cancellationToken: cancellationToken);
        return File.Exists(FlingArchiveJsonPath) && File.Exists(FlingMainJsonPath);
    }

    public async Task<bool> FetchXiaoXingDataAsync(CancellationToken cancellationToken = default)
    {
        var signedUrl = await _downloadService.GetSignedDownloadUrlAsync("GCM/Data/xiaoxing.json", cancellationToken);
        if (string.IsNullOrWhiteSpace(signedUrl))
        {
            return false;
        }

        _ = await _downloadService.DownloadFileAsync(signedUrl, AppPaths.DatabaseDirectory, cancellationToken: cancellationToken);
        return File.Exists(XiaoXingPath);
    }

    public async Task<bool> FetchCheatTableDataAsync(CancellationToken cancellationToken = default)
    {
        var signedUrl = await _downloadService.GetSignedDownloadUrlAsync("GCM/Data/cheat_table.json", cancellationToken);
        if (string.IsNullOrWhiteSpace(signedUrl))
        {
            return false;
        }

        _ = await _downloadService.DownloadFileAsync(signedUrl, AppPaths.DatabaseDirectory, cancellationToken: cancellationToken);
        return File.Exists(CheatTablePath);
    }

    public async Task<IReadOnlyList<TrainerCatalogEntry>> SearchAsync(string keyword, AppSettings settings, CancellationToken cancellationToken = default)
    {
        var keywordList = await TranslateKeywordAsync(keyword, cancellationToken);
        if (keywordList.Count == 0)
        {
            return [];
        }

        var results = new List<TrainerCatalogEntry>();
        results.AddRange(SearchFromFlingArchive(keywordList, settings));
        results.AddRange(SearchFromFlingMain(keywordList, settings));
        results = DeduplicateFlingEntries(results);

        if (settings.EnableXiaoXing)
        {
            results.AddRange(SearchJsonCatalog(XiaoXingPath, keywordList, static entry => new TrainerCatalogEntry
            {
                GameName = ReadString(entry, "game_name"),
                Origin = "xiaoxing",
                Url = ReadString(entry, "gcm_url"),
                Version = ReadString(entry, "version")
            }));
        }

        if (settings.EnableCT)
        {
            results.AddRange(SearchJsonCatalog(CheatTablePath, keywordList, static entry => new TrainerCatalogEntry
            {
                GameName = ReadString(entry, "game_name"),
                Origin = "the_cheat_script",
                Url = ReadString(entry, "gcm_url"),
                Version = ReadString(entry, "version"),
                Author = ReadString(entry, "author"),
                CustomName = ReadString(entry, "custom_name"),
                CustomNameEn = ReadString(entry, "custom_name_en"),
                CustomNameZh = ReadString(entry, "custom_name_zh")
            }));
        }

        if (settings.EnableGCM)
        {
            results.AddRange(SearchJsonCatalog(GcmPath, keywordList, static entry => new TrainerCatalogEntry
            {
                GameName = ReadString(entry, "game_name"),
                Origin = ReadString(entry, "origin"),
                Url = ReadString(entry, "gcm_url"),
                Version = ReadString(entry, "version"),
                Author = ReadString(entry, "author"),
                CustomName = ReadString(entry, "custom_name"),
                CustomNameEn = ReadString(entry, "custom_name_en"),
                CustomNameZh = ReadString(entry, "custom_name_zh"),
                Extension = ReadString(entry, "extension")
            }, allowCustomNameOnlyMatch: true));
        }

        foreach (var trainer in results)
        {
            trainer.DisplayName = await TranslateTrainerNameAsync(trainer, settings, cancellationToken);
        }

        return results
            .Where(static trainer => !string.IsNullOrWhiteSpace(trainer.DisplayName))
            .OrderBy(
                trainer => settings.SortByOrigin ? trainer.DisplayName : PrefixRegex.Replace(trainer.DisplayName, string.Empty),
                Comparer<string>.Create((left, right) => StringUtilities.CompareDisplayNames(left, right, settings.Language)))
            .ToList();
    }

    public async Task<IReadOnlyList<TrainerCatalogEntry>> CheckTrainerUpdatesAsync(
        IEnumerable<InstalledTrainer> installedTrainers,
        AppSettings settings,
        bool autoCheck,
        CancellationToken cancellationToken = default)
    {
        var updates = new List<TrainerCatalogEntry>();
        foreach (var trainer in installedTrainers)
        {
            var infoPath = Path.Combine(trainer.RootPath, "gcm_info.json");
            if (!File.Exists(infoPath))
            {
                continue;
            }

            using var infoJson = JsonDocument.Parse(await File.ReadAllTextAsync(infoPath, cancellationToken));
            var root = infoJson.RootElement;
            var origin = root.TryGetProperty("origin", out var originProperty) ? originProperty.GetString() ?? string.Empty : string.Empty;
            var gameName = root.TryGetProperty("game_name", out var nameProperty) ? nameProperty.GetString() ?? string.Empty : string.Empty;
            var currentVersion = root.TryGetProperty("version", out var versionProperty) ? versionProperty.GetString() ?? string.Empty : string.Empty;
            var storedUrl = root.TryGetProperty("gcm_url", out var urlProperty) ? urlProperty.GetString() ?? string.Empty : string.Empty;
            if (string.IsNullOrWhiteSpace(gameName) || string.IsNullOrWhiteSpace(currentVersion))
            {
                continue;
            }

            var databasePath = GetUpdateDatabasePath(origin, settings, autoCheck);
            if (string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath))
            {
                continue;
            }

            using var databaseJson = JsonDocument.Parse(await File.ReadAllTextAsync(databasePath, cancellationToken));
            foreach (var entry in databaseJson.RootElement.EnumerateArray())
            {
                if (!string.Equals(ReadString(entry, "game_name"), gameName, StringComparison.Ordinal))
                {
                    continue;
                }

                if ((origin is "other" or "ct_other") &&
                    !string.Equals(ReadString(entry, "gcm_url"), storedUrl, StringComparison.Ordinal))
                {
                    continue;
                }

                var newVersion = ReadString(entry, "version");
                if (string.IsNullOrWhiteSpace(newVersion) || string.Equals(newVersion, currentVersion, StringComparison.Ordinal))
                {
                    break;
                }

                var updateEntry = new TrainerCatalogEntry
                {
                    GameName = gameName,
                    DisplayName = trainer.DisplayName,
                    Origin = origin,
                    Url = ReadString(entry, "gcm_url"),
                    Version = newVersion,
                    TrainerDirectory = trainer.RootPath,
                    Author = ReadString(entry, "author"),
                    CustomName = ReadString(entry, "custom_name"),
                    CustomNameEn = ReadString(entry, "custom_name_en"),
                    CustomNameZh = ReadString(entry, "custom_name_zh"),
                    Extension = ReadString(entry, "extension")
                };
                updates.Add(updateEntry);
                break;
            }
        }

        return updates;
    }

    public async Task<string> TranslateTrainerNameAsync(TrainerCatalogEntry trainer, AppSettings settings, CancellationToken cancellationToken = default)
    {
        var languageKey = settings.Language is "zh_CN" or "zh_TW" && !settings.EnSearchResults ? "zh" : "en";
        var prefix = trainer.Author.Length > 0
            ? $"[{trainer.Author}]"
            : $"[{GetPrefix(languageKey, trainer.Origin)}]";

        if (string.Equals(trainer.GameName, "none", StringComparison.OrdinalIgnoreCase))
        {
            var displayName = languageKey == "zh"
                ? trainer.CustomNameZh ?? trainer.CustomName
                : trainer.CustomNameEn ?? trainer.CustomName;
            return string.IsNullOrWhiteSpace(displayName) ? prefix : $"{prefix} {displayName}".Trim();
        }

        var translatedGameName = await FindBestTrainerMatchAsync(trainer.GameName, languageKey, cancellationToken) ?? trainer.GameName;
        var suffix = languageKey == "zh"
            ? FirstNonEmpty(trainer.CustomNameZh, trainer.CustomName, "Trainer")
            : FirstNonEmpty(trainer.CustomNameEn, trainer.CustomName, "Trainer");

        return $"{prefix} {translatedGameName} {suffix}".Trim();
    }

    private List<TrainerCatalogEntry> SearchFromFlingArchive(IReadOnlyCollection<string> keywordList, AppSettings settings)
    {
        var results = new List<TrainerCatalogEntry>();

        if (string.Equals(settings.FlingDownloadServer, "official", StringComparison.OrdinalIgnoreCase))
        {
            if (!File.Exists(FlingArchiveHtmlPath))
            {
                return results;
            }

            var document = new HtmlDocument();
            document.LoadHtml(File.ReadAllText(FlingArchiveHtmlPath));
            foreach (var node in document.DocumentNode.SelectNodes("//*[@target='_self']") ?? Enumerable.Empty<HtmlNode>())
            {
                var rawTrainerName = node.InnerText.Trim();
                var gameName = Regex.Replace(
                    rawTrainerName,
                    @" v[\d.]+.*|\.\bv.*| \d+\.\d+\.\d+.*| Plus\s\d+.*|Build\s\d+.*|(\d+\.\d+-Update.*)|Update\s\d+.*|\(Update\s.*| Early Access .*|\.Early.Access.*",
                    string.Empty).Replace("_", ": ", StringComparison.Ordinal).Trim();

                if (!StringUtilities.KeywordMatch(keywordList, gameName))
                {
                    continue;
                }

                var href = node.GetAttributeValue("href", string.Empty);
                results.Add(new TrainerCatalogEntry
                {
                    GameName = gameName,
                    Origin = "fling_archive",
                    Url = new Uri(new Uri("https://archive.flingtrainer.com/"), href).ToString()
                });
            }
        }
        else
        {
            results.AddRange(SearchJsonCatalog(FlingArchiveJsonPath, keywordList, static entry => new TrainerCatalogEntry
            {
                GameName = ReadString(entry, "game_name"),
                Origin = "fling_archive",
                Url = ReadString(entry, "gcm_url")
            }));
        }

        return results;
    }

    private List<TrainerCatalogEntry> SearchFromFlingMain(IReadOnlyCollection<string> keywordList, AppSettings settings)
    {
        var results = new List<TrainerCatalogEntry>();

        if (string.Equals(settings.FlingDownloadServer, "official", StringComparison.OrdinalIgnoreCase))
        {
            if (!File.Exists(FlingMainHtmlPath))
            {
                return results;
            }

            var document = new HtmlDocument();
            document.LoadHtml(File.ReadAllText(FlingMainHtmlPath));
            foreach (var node in document.DocumentNode.SelectNodes("//div[contains(@class,'letter-section')]//li//a") ?? Enumerable.Empty<HtmlNode>())
            {
                var rawTrainerName = node.InnerText.Trim();
                var gameName = rawTrainerName.EndsWith(" Trainer", StringComparison.OrdinalIgnoreCase)
                    ? rawTrainerName[..^" Trainer".Length]
                    : rawTrainerName;
                if (!StringUtilities.KeywordMatch(keywordList, gameName))
                {
                    continue;
                }

                results.Add(new TrainerCatalogEntry
                {
                    GameName = gameName,
                    Origin = "fling_main",
                    Url = node.GetAttributeValue("href", string.Empty)
                });
            }
        }
        else
        {
            results.AddRange(SearchJsonCatalog(FlingMainJsonPath, keywordList, static entry => new TrainerCatalogEntry
            {
                GameName = ReadString(entry, "game_name"),
                Origin = "fling_main",
                Url = ReadString(entry, "gcm_url"),
                Version = ReadString(entry, "version")
            }));
        }

        return results;
    }

    private List<TrainerCatalogEntry> SearchJsonCatalog(
        string path,
        IReadOnlyCollection<string> keywordList,
        Func<JsonElement, TrainerCatalogEntry> factory,
        bool allowCustomNameOnlyMatch = false)
    {
        var results = new List<TrainerCatalogEntry>();
        if (!File.Exists(path))
        {
            return results;
        }

        using var json = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var entry in json.RootElement.EnumerateArray())
        {
            var gameName = ReadString(entry, "game_name");
            var matched = !string.IsNullOrWhiteSpace(gameName) && StringUtilities.KeywordMatch(keywordList, gameName);
            if (!matched && allowCustomNameOnlyMatch && string.Equals(gameName, "none", StringComparison.OrdinalIgnoreCase))
            {
                var targets = new[]
                {
                    ReadString(entry, "custom_name_en"),
                    ReadString(entry, "custom_name_zh"),
                    ReadString(entry, "custom_name")
                }.Where(static value => !string.IsNullOrWhiteSpace(value));
                matched = targets.Any(target => StringUtilities.KeywordMatch(keywordList, target));
            }

            if (matched)
            {
                results.Add(factory(entry));
            }
        }

        return results;
    }

    private static List<TrainerCatalogEntry> DeduplicateFlingEntries(IEnumerable<TrainerCatalogEntry> input)
    {
        var map = new Dictionary<string, TrainerCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var trainer in input)
        {
            var key = StringUtilities.Sanitize(trainer.GameName);
            if (!map.TryGetValue(key, out var existing))
            {
                map[key] = trainer;
                continue;
            }

            if (trainer.Origin == "fling_main" && existing.Origin == "fling_archive")
            {
                map[key] = trainer;
            }
        }

        return map.Values.ToList();
    }

    private async Task<List<string>> TranslateKeywordAsync(string keyword, CancellationToken cancellationToken)
    {
        var translations = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { keyword };
        if (!File.Exists(TranslationsPath))
        {
            return translations.ToList();
        }

        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(TranslationsPath, cancellationToken));
        var sanitizedKeyword = StringUtilities.Sanitize(keyword);
        foreach (var entry in json.RootElement.EnumerateArray())
        {
            var english = ReadString(entry, "en_US");
            var chinese = ReadString(entry, "zh_CN");
            if (StringUtilities.IsChinese(keyword))
            {
                if (StringUtilities.Sanitize(chinese).Contains(sanitizedKeyword, StringComparison.OrdinalIgnoreCase))
                {
                    translations.Add(english);
                }
            }
            else
            {
                if (StringUtilities.Sanitize(english).Contains(sanitizedKeyword, StringComparison.OrdinalIgnoreCase))
                {
                    translations.Add(chinese);
                }
            }
        }

        return translations.Where(static value => !string.IsNullOrWhiteSpace(value)).ToList();
    }

    private async Task<string?> FindBestTrainerMatchAsync(string targetName, string targetLanguage, CancellationToken cancellationToken)
    {
        if (!File.Exists(TranslationsPath))
        {
            return null;
        }

        if (StringUtilities.IsChinese(targetName) && targetLanguage == "zh")
        {
            return null;
        }

        if (!StringUtilities.IsChinese(targetName) && targetLanguage == "en")
        {
            return null;
        }

        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(TranslationsPath, cancellationToken));
        var sanitizedTarget = StringUtilities.Sanitize(targetName);
        var bestScore = -1;
        string? bestMatch = null;
        foreach (var entry in json.RootElement.EnumerateArray())
        {
            var english = ReadString(entry, "en_US");
            var chinese = ReadString(entry, "zh_CN");
            var source = targetLanguage == "zh" ? english : chinese;
            var destination = targetLanguage == "zh" ? chinese : english;
            var score = FuzzySharp.Fuzz.Ratio(StringUtilities.Sanitize(source), sanitizedTarget);
            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = destination;
            }
        }

        return bestScore >= 85 ? bestMatch : null;
    }

    private static string GetUpdateDatabasePath(string origin, AppSettings settings, bool autoCheck) =>
        origin switch
        {
            "gcm" or "other" or "ct_other" when !autoCheck || settings.AutoUpdateGCMTrainers => Path.Combine(AppPaths.DatabaseDirectory, "gcm_trainers.json"),
            "fling_main" when !autoCheck || settings.AutoUpdateFlingTrainers => Path.Combine(AppPaths.DatabaseDirectory, "fling_main.json"),
            "xiaoxing" when !autoCheck || settings.AutoUpdateXiaoXingTrainers => Path.Combine(AppPaths.DatabaseDirectory, "xiaoxing.json"),
            "the_cheat_script" when !autoCheck || settings.AutoUpdateCTTrainers => Path.Combine(AppPaths.DatabaseDirectory, "cheat_table.json"),
            _ => string.Empty
        };

    private static string GetPrefix(string languageKey, string origin) =>
        (languageKey, origin) switch
        {
            ("zh", "fling_main") => "FL",
            ("zh", "fling_archive") => "FL",
            ("zh", "xiaoxing") => "XX",
            ("zh", "the_cheat_script") => "CT",
            ("zh", "ct_other") => "CT",
            ("zh", "gcm") => "GCM",
            ("zh", "other") => "OT",
            ("en", "fling_main") => "FL",
            ("en", "fling_archive") => "FL",
            ("en", "xiaoxing") => "XX",
            ("en", "the_cheat_script") => "CT",
            ("en", "ct_other") => "CT",
            ("en", "gcm") => "GCM",
            ("en", "other") => "OT",
            _ => "OT"
        };

    private static string ReadString(JsonElement entry, string propertyName) =>
        entry.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.ToString() ?? string.Empty
            : string.Empty;

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
