using System.Text;
using System.Text.Json;
using GameCheatsManager.WinUI.Interop;
using GameCheatsManager.WinUI.Models;
using Microsoft.Win32;

namespace GameCheatsManager.WinUI.Services;

public sealed class TrainerLibraryService
{
    private static readonly string[] DefaultExtensions = [".exe", ".ct", ".cetrainer"];
    private static readonly string[] ExeExclusions = ["flashplayer_22.0.0.210_ax_debug.exe"];
    private readonly ProcessService _processService;

    public TrainerLibraryService(ProcessService processService)
    {
        _processService = processService;
    }

    public IReadOnlyList<InstalledTrainer> GetInstalledTrainers(string downloadPath, string language)
    {
        Directory.CreateDirectory(downloadPath);

        var trainers = new List<InstalledTrainer>();
        foreach (var entry in Directory.EnumerateFileSystemEntries(downloadPath).OrderBy(static item => item, StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(entry))
            {
                var extension = Path.GetExtension(entry);
                if (!DefaultExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fileInfo = new FileInfo(entry);
                if (fileInfo.Length == 0)
                {
                    continue;
                }

                trainers.Add(new InstalledTrainer
                {
                    DisplayName = Path.GetFileNameWithoutExtension(entry),
                    LaunchPath = entry,
                    RootPath = entry,
                    Extension = extension
                });
                continue;
            }

            if (!Directory.Exists(entry))
            {
                continue;
            }

            var info = LoadInfo(Path.Combine(entry, "gcm_info.json"));
            var customExtension = info.TryGetValue("extension", out var extValue) ? extValue : string.Empty;
            if (string.Equals(customExtension, "none", StringComparison.OrdinalIgnoreCase))
            {
                trainers.Add(CreateInstalledTrainer(Path.GetFileName(entry), entry, entry, info, true));
                continue;
            }

            var targetExtensions = !string.IsNullOrWhiteSpace(customExtension)
                ? new[] { "." + customExtension.TrimStart('.') }
                : DefaultExtensions;

            string? matchedPath = null;
            foreach (var child in Directory.EnumerateFiles(entry))
            {
                var childExtension = Path.GetExtension(child);
                if (targetExtensions.Contains(childExtension, StringComparer.OrdinalIgnoreCase) &&
                    !ExeExclusions.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase))
                {
                    matchedPath = child;
                    break;
                }
            }

            if (matchedPath is not null || info.Count > 0)
            {
                trainers.Add(CreateInstalledTrainer(Path.GetFileName(entry), matchedPath ?? entry, entry, info, matchedPath is null));
            }
        }

        return trainers
            .OrderBy(static trainer => trainer.DisplayName, StringComparer.Create(
                language switch
                {
                    "zh_CN" => new System.Globalization.CultureInfo("zh-CN"),
                    "zh_TW" => new System.Globalization.CultureInfo("zh-TW"),
                    "de_DE" => new System.Globalization.CultureInfo("de-DE"),
                    _ => new System.Globalization.CultureInfo("en-US")
                },
                ignoreCase: true))
            .ToList();
    }

    public IReadOnlyList<InstalledTrainer> FilterInstalledTrainers(IEnumerable<InstalledTrainer> installedTrainers, string searchText) =>
        string.IsNullOrWhiteSpace(searchText)
            ? installedTrainers.ToList()
            : installedTrainers
                .Where(trainer => trainer.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .ToList();

    public async Task<LaunchTrainerResult> LaunchTrainerAsync(InstalledTrainer trainer, bool safePath, bool showCePrompt, nint ownerWindow = default)
    {
        if (trainer.IsDirectoryOnly || Directory.Exists(trainer.LaunchPath))
        {
            _processService.OpenWithShell(trainer.LaunchPath);
            return new LaunchTrainerResult { Started = true };
        }

        var originalPath = trainer.LaunchPath;
        var launchPath = safePath ? await GetAsciiLaunchPathAsync(originalPath) : originalPath;
        var extension = Path.GetExtension(originalPath);

        if ((string.Equals(extension, ".ct", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(extension, ".cetrainer", StringComparison.OrdinalIgnoreCase)) &&
            showCePrompt &&
            !IsCheatEngineAvailable(extension))
        {
            return new LaunchTrainerResult
            {
                Started = false,
                RequiresCheatEnginePrompt = true
            };
        }

        var shellResult = await Task.Run(() => _processService.ShellLaunch(
            launchPath,
            string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase),
            Path.GetDirectoryName(launchPath),
            ownerWindow));

        return new LaunchTrainerResult
        {
            Started = shellResult.Started,
            Message = shellResult.Message
        };
    }

    public void CleanupLaunchJunctions()
    {
        var tempPath = Path.GetTempPath();
        foreach (var path in Directory.EnumerateDirectories(tempPath, "GCM_Launch_*"))
        {
            try
            {
                Directory.Delete(path);
            }
            catch
            {
            }
        }
    }

    public async Task DeleteTrainerAsync(InstalledTrainer trainer)
    {
        if (Directory.Exists(trainer.RootPath))
        {
            await Task.Run(() => Directory.Delete(trainer.RootPath, recursive: true));
            return;
        }

        if (File.Exists(trainer.RootPath))
        {
            File.SetAttributes(trainer.RootPath, FileAttributes.Normal);
            File.Delete(trainer.RootPath);
        }
    }

    public async Task ImportFilesAsync(IEnumerable<string> fileNames, string downloadPath)
    {
        Directory.CreateDirectory(downloadPath);
        foreach (var fileName in fileNames)
        {
            var destination = Path.Combine(downloadPath, Path.GetFileName(fileName));
            await using var source = File.OpenRead(fileName);
            await using var destinationStream = File.Create(destination);
            await source.CopyToAsync(destinationStream);
        }
    }

    public async Task MoveDirectoryAsync(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(destinationPath);
        foreach (var entry in Directory.EnumerateFileSystemEntries(sourcePath))
        {
            var destination = Path.Combine(destinationPath, Path.GetFileName(entry));
            if (Directory.Exists(entry))
            {
                Directory.Move(entry, destination);
            }
            else
            {
                if (File.Exists(destination))
                {
                    File.SetAttributes(destination, FileAttributes.Normal);
                    File.Delete(destination);
                }

                File.Move(entry, destination);
            }
        }

        await Task.Run(() => Directory.Delete(sourcePath, recursive: true));
    }

    public bool IsCheatEngineAvailable(string extension)
    {
        if (TryResolveProgIdAssociation($@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}\UserChoice", "ProgId", out var userChoiceCommand) &&
            userChoiceCommand.Contains("cheatengine", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (TryResolveExtensionAssociation(extension, out var standardCommand) &&
            standardCommand.Contains("cheatengine", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool TryResolveProgIdAssociation(string subKeyPath, string valueName, out string command)
    {
        command = string.Empty;
        try
        {
            using var userChoiceKey = Registry.CurrentUser.OpenSubKey(subKeyPath);
            var progId = userChoiceKey?.GetValue(valueName)?.ToString();
            if (string.IsNullOrWhiteSpace(progId))
            {
                return false;
            }

            using var commandKey = Registry.ClassesRoot.OpenSubKey($@"{progId}\shell\open\command");
            command = commandKey?.GetValue(string.Empty)?.ToString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(command);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryResolveExtensionAssociation(string extension, out string command)
    {
        command = string.Empty;
        try
        {
            using var extensionKey = Registry.ClassesRoot.OpenSubKey(extension);
            var progId = extensionKey?.GetValue(string.Empty)?.ToString();
            if (string.IsNullOrWhiteSpace(progId))
            {
                return false;
            }

            using var commandKey = Registry.ClassesRoot.OpenSubKey($@"{progId}\shell\open\command");
            command = commandKey?.GetValue(string.Empty)?.ToString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(command);
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> GetAsciiLaunchPathAsync(string originalPath)
    {
        if (Encoding.UTF8.GetByteCount(originalPath) == originalPath.Length)
        {
            return originalPath;
        }

        var shortBufferLength = Win32Native.GetShortPathName(originalPath, null, 0);
        if (shortBufferLength > 0)
        {
            var shortBuffer = new StringBuilder((int)shortBufferLength);
            _ = Win32Native.GetShortPathName(originalPath, shortBuffer, shortBufferLength);
            var shortPath = shortBuffer.ToString();
            if (Encoding.UTF8.GetByteCount(shortPath) == shortPath.Length)
            {
                return shortPath;
            }
        }

        var directory = Path.GetDirectoryName(originalPath)!;
        var fileName = Path.GetFileName(originalPath);
        var hash = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(directory))).ToLowerInvariant()[..8];
        var junctionPath = Path.Combine(Path.GetTempPath(), $"GCM_Launch_{hash}");
        if (!Directory.Exists(junctionPath))
        {
            var (exitCode, _, _) = await _processService.RunAsync("cmd.exe", ["/c", "mklink", "/J", junctionPath, directory]);
            if (exitCode != 0 || !Directory.Exists(junctionPath))
            {
                return originalPath;
            }
        }

        return Path.Combine(junctionPath, fileName);
    }

    private static Dictionary<string, string> LoadInfo(string path)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ??
                   new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static InstalledTrainer CreateInstalledTrainer(
        string displayName,
        string launchPath,
        string rootPath,
        Dictionary<string, string> info,
        bool isDirectoryOnly)
    {
        info.TryGetValue("origin", out var origin);
        info.TryGetValue("version", out var version);
        info.TryGetValue("gcm_url", out var gcmUrl);
        info.TryGetValue("extension", out var extension);

        return new InstalledTrainer
        {
            DisplayName = displayName,
            LaunchPath = launchPath,
            RootPath = rootPath,
            Origin = origin ?? string.Empty,
            Version = version ?? string.Empty,
            GcmUrl = gcmUrl ?? string.Empty,
            Extension = extension ?? Path.GetExtension(launchPath),
            IsDirectoryOnly = isDirectoryOnly
        };
    }
}
