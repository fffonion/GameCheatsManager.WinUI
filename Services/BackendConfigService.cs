using System.Text.RegularExpressions;
using GameCheatsManager.WinUI.Models;

namespace GameCheatsManager.WinUI.Services;

public sealed class BackendConfigService
{
    private static readonly Regex AssignmentRegex = new(
        "^(?<name>[A-Z0-9_]+)\\s*=\\s*[\"'](?<value>.*?)[\"']\\s*$",
        RegexOptions.Compiled);

    public BackendConfig Load()
    {
        var config = new BackendConfig
        {
            SignedUrlDownloadEndpoint = Environment.GetEnvironmentVariable("SIGNED_URL_DOWNLOAD_ENDPOINT") ?? string.Empty,
            SignedUrlUploadEndpoint = Environment.GetEnvironmentVariable("SIGNED_URL_UPLOAD_ENDPOINT") ?? string.Empty,
            VersionCheckerEndpoint = Environment.GetEnvironmentVariable("VERSION_CHECKER_ENDPOINT") ?? string.Empty,
            PatchPatternsEndpoint = Environment.GetEnvironmentVariable("PATCH_PATTERNS_ENDPOINT") ?? string.Empty,
            ClientApiKey = Environment.GetEnvironmentVariable("CLIENT_API_KEY") ?? string.Empty
        };

        foreach (var candidate in GetSecretConfigCandidates())
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            foreach (var line in File.ReadLines(candidate))
            {
                var match = AssignmentRegex.Match(line.Trim());
                if (!match.Success)
                {
                    continue;
                }

                var name = match.Groups["name"].Value;
                var value = match.Groups["value"].Value;
                switch (name)
                {
                    case "SIGNED_URL_DOWNLOAD_ENDPOINT" when string.IsNullOrWhiteSpace(config.SignedUrlDownloadEndpoint):
                        config.SignedUrlDownloadEndpoint = value;
                        break;
                    case "SIGNED_URL_UPLOAD_ENDPOINT" when string.IsNullOrWhiteSpace(config.SignedUrlUploadEndpoint):
                        config.SignedUrlUploadEndpoint = value;
                        break;
                    case "VERSION_CHECKER_ENDPOINT" when string.IsNullOrWhiteSpace(config.VersionCheckerEndpoint):
                        config.VersionCheckerEndpoint = value;
                        break;
                    case "PATCH_PATTERNS_ENDPOINT" when string.IsNullOrWhiteSpace(config.PatchPatternsEndpoint):
                        config.PatchPatternsEndpoint = value;
                        break;
                    case "CLIENT_API_KEY" when string.IsNullOrWhiteSpace(config.ClientApiKey):
                        config.ClientApiKey = value;
                        break;
                }
            }
        }

        return config;
    }

    private static IEnumerable<string> GetSecretConfigCandidates()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in EnumerateConfigCandidates(AppPaths.AppRoot))
        {
            if (seen.Add(candidate))
            {
                yield return candidate;
            }
        }

        foreach (var candidate in EnumerateConfigCandidates(AppContext.BaseDirectory))
        {
            if (seen.Add(candidate))
            {
                yield return candidate;
            }
        }

        foreach (var candidate in EnumerateConfigCandidates(Directory.GetCurrentDirectory()))
        {
            if (seen.Add(candidate))
            {
                yield return candidate;
            }
        }
    }

    private static IEnumerable<string> EnumerateConfigCandidates(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            yield return Path.Combine(current.FullName, "secret_config.py");
            current = current.Parent;
        }
    }
}
