using System.Text.Json;
using GameCheatsManager.WinUI.Models;

namespace GameCheatsManager.WinUI.Services;

public sealed class UpdateService
{
    private readonly FileDownloadService _downloadService;

    public UpdateService(FileDownloadService downloadService)
    {
        _downloadService = downloadService;
    }

    public async Task<AppAnnouncement?> FetchLatestAnnouncementAsync(string lastSeenAnnouncementId, CancellationToken cancellationToken = default)
    {
        var signedUrl = await _downloadService.GetSignedDownloadUrlAsync("GCM/Data/announcement.json", cancellationToken);
        if (string.IsNullOrWhiteSpace(signedUrl))
        {
            return null;
        }

        var jsonText = await _downloadService.GetWebPageContentAsync(signedUrl, cancellationToken);
        using var document = JsonDocument.Parse(jsonText);
        if (!document.RootElement.TryGetProperty("announcements", out var announcements) ||
            announcements.ValueKind != JsonValueKind.Array ||
            announcements.GetArrayLength() == 0)
        {
            return null;
        }

        var latest = announcements.EnumerateArray().Last();
        var id = latest.TryGetProperty("id", out var idProperty) ? idProperty.GetString() ?? string.Empty : string.Empty;
        if (string.Equals(id, lastSeenAnnouncementId, StringComparison.Ordinal))
        {
            return null;
        }

        return new AppAnnouncement
        {
            Id = id,
            TitleEn = ReadString(latest, "title_en"),
            TitleZh = ReadString(latest, "title_zh"),
            MessageEn = ReadString(latest, "message_en"),
            MessageZh = ReadString(latest, "message_zh")
        };
    }

    public Task<string?> GetLatestVersionAsync(string appName, CancellationToken cancellationToken = default) =>
        _downloadService.GetLatestVersionAsync(appName, cancellationToken);

    public static bool IsNewerVersion(string latestVersion, string currentVersion) =>
        CompareVersionParts(latestVersion, currentVersion) > 0;

    private static int CompareVersionParts(string left, string right)
    {
        var leftTokens = Tokenize(left);
        var rightTokens = Tokenize(right);
        var count = Math.Max(leftTokens.Count, rightTokens.Count);
        for (var index = 0; index < count; index++)
        {
            var leftToken = index < leftTokens.Count ? leftTokens[index] : "0";
            var rightToken = index < rightTokens.Count ? rightTokens[index] : "0";

            if (int.TryParse(leftToken, out var leftNumber) && int.TryParse(rightToken, out var rightNumber))
            {
                if (leftNumber != rightNumber)
                {
                    return leftNumber.CompareTo(rightNumber);
                }
            }
            else
            {
                var comparison = string.Compare(leftToken, rightToken, StringComparison.OrdinalIgnoreCase);
                if (comparison != 0)
                {
                    return comparison;
                }
            }
        }

        return 0;
    }

    private static List<string> Tokenize(string version) =>
        version
            .Replace("-", ".", StringComparison.Ordinal)
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    private static string ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) ? property.ToString() ?? string.Empty : string.Empty;
}
