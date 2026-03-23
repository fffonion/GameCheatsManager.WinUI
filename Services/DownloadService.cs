using System.Text.Json;
using System.Text.RegularExpressions;
using GameCheatsManager.WinUI.Models;
using HtmlAgilityPack;

namespace GameCheatsManager.WinUI.Services;

public sealed class DownloadService
{
    private static readonly string[] SupportedArchives = [".zip", ".rar", ".7z"];
    private static readonly string[] ExeExclusions = ["flashplayer_22.0.0.210_ax_debug.exe"];
    private readonly FileDownloadService _downloadService;
    private readonly ProcessService _processService;
    private readonly TrainerCatalogService _catalogService;

    public DownloadService(
        FileDownloadService downloadService,
        ProcessService processService,
        TrainerCatalogService catalogService)
    {
        _downloadService = downloadService;
        _processService = processService;
        _catalogService = catalogService;
    }

    public async Task<DownloadResult> DownloadTrainerAsync(
        TrainerCatalogEntry trainer,
        IReadOnlyCollection<InstalledTrainer> installedTrainers,
        AppSettings settings,
        IProgress<string>? status = null,
        IProgress<DownloadProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        status?.Report("Checking for internet connection...");
        if (!await _downloadService.IsInternetConnectedAsync(cancellationToken))
        {
            return new DownloadResult { Success = false, Message = "No internet connection, download failed." };
        }

        PrepareDownloadTemp();

        var moveList = new List<MoveItem>();
        string? instructionDirectory = null;

        try
        {
            switch (trainer.Origin)
            {
                case "fling_main":
                case "fling_archive":
                    (moveList, instructionDirectory) = await PrepareFlingDownloadAsync(
                        trainer, installedTrainers, settings, status, progress, cancellationToken);
                    break;
                case "xiaoxing":
                    if (!_downloadService.HasSignedDownloadBackend)
                    {
                        return new DownloadResult { Success = false, Message = "This source requires the private Game-Zone backend configuration, which is not present in this workspace." };
                    }

                    (moveList, instructionDirectory) = await PrepareXiaoXingDownloadAsync(
                        trainer, installedTrainers, settings, status, progress, cancellationToken);
                    break;
                case "gcm":
                case "other":
                case "ct_other":
                case "the_cheat_script":
                    if (!_downloadService.HasSignedDownloadBackend)
                    {
                        return new DownloadResult { Success = false, Message = "This source requires the private Game-Zone backend configuration, which is not present in this workspace." };
                    }

                    (moveList, instructionDirectory) = await PrepareDefaultDownloadAsync(
                        trainer, installedTrainers, settings, status, progress, cancellationToken);
                    break;
                default:
                    return new DownloadResult { Success = false, Message = $"Unsupported trainer origin: {trainer.Origin}" };
            }
        }
        catch (Exception ex)
        {
            return new DownloadResult { Success = false, Message = ex.Message };
        }

        if (moveList.Count == 0)
        {
            return new DownloadResult { Success = false, Message = "No trainer files were prepared for installation." };
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(trainer.TrainerDirectory) && Directory.Exists(trainer.TrainerDirectory))
            {
                Directory.Delete(trainer.TrainerDirectory, recursive: true);
            }

            foreach (var item in moveList)
            {
                if (File.Exists(item.Source))
                {
                    var destinationDirectory = Path.GetDirectoryName(item.Destination)!;
                    Directory.CreateDirectory(destinationDirectory);
                    if (File.Exists(item.Destination))
                    {
                        File.SetAttributes(item.Destination, FileAttributes.Normal);
                        File.Delete(item.Destination);
                    }

                    File.Move(item.Source, item.Destination);
                    if (!string.Equals(destinationDirectory, instructionDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        await WriteGcmInfoAsync(destinationDirectory, trainer, cancellationToken);
                    }
                }
                else if (Directory.Exists(item.Source))
                {
                    if (Directory.Exists(item.Destination))
                    {
                        Directory.Delete(item.Destination, recursive: true);
                    }

                    Directory.Move(item.Source, item.Destination);
                    if (!string.Equals(item.Destination, instructionDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        await WriteGcmInfoAsync(item.Destination, trainer, cancellationToken);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return new DownloadResult { Success = false, Message = $"An error occurred when moving trainer: {ex.Message}" };
        }

        return new DownloadResult
        {
            Success = true,
            Message = "Download success!",
            InstructionDirectory = string.IsNullOrWhiteSpace(instructionDirectory) ? null : instructionDirectory
        };
    }

    private async Task<(List<MoveItem>, string?)> PrepareFlingDownloadAsync(
        TrainerCatalogEntry trainer,
        IReadOnlyCollection<InstalledTrainer> installedTrainers,
        AppSettings settings,
        IProgress<string>? status,
        IProgress<DownloadProgressInfo>? progress,
        CancellationToken cancellationToken)
    {
        var isUpdate = !string.IsNullOrWhiteSpace(trainer.TrainerDirectory);
        var displayName = isUpdate ? trainer.DisplayName : StringUtilities.SymbolReplacement(trainer.DisplayName);
        EnsureUniqueTrainer(displayName, installedTrainers, isUpdate);

        status?.Report(isUpdate ? $"Updating {displayName}..." : "Downloading...");

        var targetUrl = trainer.Url;
        if (string.Equals(settings.FlingDownloadServer, "official", StringComparison.OrdinalIgnoreCase) &&
            !isUpdate &&
            trainer.Origin == "fling_main")
        {
            var pageHtml = await _downloadService.GetWebPageContentAsync(targetUrl, cancellationToken);
            var pageDocument = new HtmlDocument();
            pageDocument.LoadHtml(pageHtml);
            var targetNode = pageDocument.DocumentNode
                .SelectSingleNode("//a[@target='_self' and contains(@href, 'flingtrainer.com')]");
            targetUrl = targetNode?.GetAttributeValue("href", string.Empty) ?? string.Empty;
            var divEntry = pageDocument.DocumentNode.SelectSingleNode("//div[contains(@class, 'entry')]");
            if (divEntry is not null)
            {
                var match = Regex.Match(
                    divEntry.InnerText,
                    @"options.*game\s*version.*last\s*updated:\s*(\d{4}\.[0-1]?\d\.[0-3]?\d)",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (match.Success)
                {
                    trainer.Version = match.Groups[1].Value;
                }
            }
        }
        else if (!string.Equals(settings.FlingDownloadServer, "official", StringComparison.OrdinalIgnoreCase) || isUpdate)
        {
            targetUrl = await _downloadService.GetSignedDownloadUrlAsync(targetUrl, cancellationToken) ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            throw new InvalidOperationException("Failed to determine trainer download link.");
        }

        var downloadPath = await _downloadService.DownloadFileAsync(targetUrl, AppPaths.DownloadTempDirectory, progress, cancellationToken)
                           ?? throw new InvalidOperationException("Internet request failed.");
        if (SupportedArchives.Contains(Path.GetExtension(downloadPath), StringComparer.OrdinalIgnoreCase))
        {
            status?.Report("Decompressing...");
            await ExtractArchiveAsync(downloadPath, AppPaths.DownloadTempDirectory);
        }

        var extractedTrainerNames = Directory.EnumerateFiles(AppPaths.DownloadTempDirectory)
            .Where(file => Path.GetExtension(file).Equals(".exe", StringComparison.OrdinalIgnoreCase) &&
                           Path.GetFileName(file).Contains("trainer", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .OfType<string>()
            .ToList();

        var antiCheatFiles = Directory.EnumerateFileSystemEntries(AppPaths.DownloadTempDirectory)
            .Where(path =>
                !string.Equals(path, downloadPath, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Path.GetFileName(path), "info.txt", StringComparison.OrdinalIgnoreCase) &&
                !Path.GetFileName(path).Contains("trainer", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var moveList = new List<MoveItem>();
        string? instructionDirectory = null;
        if (antiCheatFiles.Count > 0)
        {
            instructionDirectory = Path.Combine(settings.DownloadPath, displayName, "gcm-instructions");
            foreach (var antiCheatFile in antiCheatFiles)
            {
                moveList.Add(new MoveItem(
                    antiCheatFile,
                    Path.Combine(instructionDirectory, Path.GetFileName(antiCheatFile))));
            }
        }

        if (extractedTrainerNames.Count == 0 && Path.GetExtension(downloadPath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            extractedTrainerNames.Add(Path.GetFileName(downloadPath));
        }

        if (extractedTrainerNames.Count == 0)
        {
            throw new InvalidOperationException("Could not find the downloaded trainer file. Try disabling antivirus and retry.");
        }

        if (extractedTrainerNames.Count > 1)
        {
            foreach (var extractedTrainerName in extractedTrainerNames)
            {
                var suffix = string.Empty;
                if (trainer.Origin == "fling_main")
                {
                    var match = Regex.Match(extractedTrainerName, @"trainer(.*)\.exe", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        suffix = match.Groups[1].Value;
                    }
                }
                else
                {
                    var match = Regex.Match(extractedTrainerName, @"\s+Update.*|\s+v\d+.*", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        suffix = match.Value.Replace(" Trainer", string.Empty, StringComparison.Ordinal).TrimEnd('.', 'e', 'x');
                    }
                }

                moveList.Insert(0, new MoveItem(
                    Path.Combine(AppPaths.DownloadTempDirectory, extractedTrainerName),
                    Path.Combine(settings.DownloadPath, $"{displayName}{suffix}", extractedTrainerName)));
            }
        }
        else
        {
            moveList.Insert(0, new MoveItem(
                Path.Combine(AppPaths.DownloadTempDirectory, extractedTrainerNames[0]),
                Path.Combine(settings.DownloadPath, displayName, extractedTrainerNames[0])));
        }

        if (settings.RemoveFlingBgMusic)
        {
            status?.Report("Removing trainer background music...");
            await ModifyFlingSettingsAsync(removeBackgroundMusic: true);
            foreach (var item in moveList.Where(static item => item.Source.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
            {
                await RemoveFlingBackgroundMusicAsync(item.Source);
            }
        }
        else
        {
            await ModifyFlingSettingsAsync(removeBackgroundMusic: false);
        }

        return (moveList, instructionDirectory);
    }

    private async Task<(List<MoveItem>, string?)> PrepareXiaoXingDownloadAsync(
        TrainerCatalogEntry trainer,
        IReadOnlyCollection<InstalledTrainer> installedTrainers,
        AppSettings settings,
        IProgress<string>? status,
        IProgress<DownloadProgressInfo>? progress,
        CancellationToken cancellationToken)
    {
        var isUpdate = !string.IsNullOrWhiteSpace(trainer.TrainerDirectory);
        var displayName = isUpdate ? trainer.DisplayName : StringUtilities.SymbolReplacement(trainer.DisplayName);
        EnsureUniqueTrainer(displayName, installedTrainers, isUpdate);

        status?.Report(isUpdate ? $"Updating {displayName}..." : "Downloading...");
        var signedUrl = await _downloadService.GetSignedDownloadUrlAsync(trainer.Url, cancellationToken)
                        ?? throw new InvalidOperationException("Failed to retrieve signed download URL.");
        var downloadPath = await _downloadService.DownloadFileAsync(signedUrl, AppPaths.DownloadTempDirectory, progress, cancellationToken)
                           ?? throw new InvalidOperationException("Internet request failed.");

        var extractDirectory = Path.Combine(AppPaths.DownloadTempDirectory, "extracted");
        status?.Report("Decompressing...");
        await ExtractArchiveAsync(downloadPath, extractDirectory);
        File.Delete(downloadPath);

        var moveList = new List<MoveItem>();
        if (!await HandleXiaoXingSpecialCasesAsync(trainer, displayName, settings.DownloadPath, extractDirectory, moveList))
        {
            moveList.Add(new MoveItem(extractDirectory, Path.Combine(settings.DownloadPath, displayName)));
        }

        if (settings.UnlockXiaoXing)
        {
            status?.Report("Patching...");
            await UnlockXiaoXingAsync(trainer, moveList);
        }

        return (moveList, null);
    }

    private async Task<(List<MoveItem>, string?)> PrepareDefaultDownloadAsync(
        TrainerCatalogEntry trainer,
        IReadOnlyCollection<InstalledTrainer> installedTrainers,
        AppSettings settings,
        IProgress<string>? status,
        IProgress<DownloadProgressInfo>? progress,
        CancellationToken cancellationToken)
    {
        var isUpdate = !string.IsNullOrWhiteSpace(trainer.TrainerDirectory);
        var displayName = isUpdate ? trainer.DisplayName : StringUtilities.SymbolReplacement(trainer.DisplayName);
        EnsureUniqueTrainer(displayName, installedTrainers, isUpdate);

        status?.Report(isUpdate ? $"Updating {displayName}..." : "Downloading...");
        var signedUrl = await _downloadService.GetSignedDownloadUrlAsync(trainer.Url, cancellationToken)
                        ?? throw new InvalidOperationException("Failed to retrieve signed download URL.");
        var downloadPath = await _downloadService.DownloadFileAsync(signedUrl, AppPaths.DownloadTempDirectory, progress, cancellationToken)
                           ?? throw new InvalidOperationException("Internet request failed.");

        var moveList = new List<MoveItem>();
        string? instructionDirectory = null;
        if (SupportedArchives.Contains(Path.GetExtension(downloadPath), StringComparer.OrdinalIgnoreCase))
        {
            status?.Report("Decompressing...");
            var extractDirectory = Path.Combine(AppPaths.DownloadTempDirectory, "extracted");
            await ExtractArchiveAsync(downloadPath, extractDirectory);
            File.Delete(downloadPath);

            var rootInstructionDirectory = Path.Combine(extractDirectory, "gcm-instructions");
            if (Directory.Exists(rootInstructionDirectory))
            {
                instructionDirectory = Path.Combine(settings.DownloadPath, displayName, "gcm-instructions");
            }

            if (!HandleMultiVersionArchive(extractDirectory, displayName, settings.DownloadPath, moveList))
            {
                moveList.Add(new MoveItem(extractDirectory, Path.Combine(settings.DownloadPath, displayName)));
            }
        }
        else
        {
            moveList.Add(new MoveItem(downloadPath, Path.Combine(settings.DownloadPath, displayName, Path.GetFileName(downloadPath))));
        }

        return (moveList, instructionDirectory);
    }

    private static void PrepareDownloadTemp()
    {
        if (Directory.Exists(AppPaths.DownloadTempDirectory))
        {
            Directory.Delete(AppPaths.DownloadTempDirectory, recursive: true);
        }

        Directory.CreateDirectory(AppPaths.DownloadTempDirectory);
    }

    private static void EnsureUniqueTrainer(string displayName, IReadOnlyCollection<InstalledTrainer> installedTrainers, bool isUpdate)
    {
        if (isUpdate)
        {
            return;
        }

        if (installedTrainers.Any(trainer => string.Equals(trainer.DisplayName, displayName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Trainer already exists, aborted download.");
        }
    }

    private async Task ExtractArchiveAsync(string archivePath, string destination)
    {
        Directory.CreateDirectory(destination);
        var (exitCode, _, stderr) = await _processService.RunAsync(
            AppPaths.SevenZipPath,
            ["x", "-y", archivePath, $"-o{destination}"]);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"An error occurred while extracting archive: {stderr}");
        }
    }

    private async Task ModifyFlingSettingsAsync(bool removeBackgroundMusic)
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var flingPath = Path.Combine(documentsPath, "FLiNGTrainer");
        var bgMusicPath = Path.Combine(flingPath, "TrainerBGM.mid");
        if (File.Exists(bgMusicPath))
        {
            if (removeBackgroundMusic)
            {
                File.Copy(AppPaths.EmptyMidiPath, bgMusicPath, overwrite: true);
            }
            else
            {
                File.Delete(bgMusicPath);
            }
        }

        foreach (var settingsFile in new[]
                 {
                     Path.Combine(flingPath, "FLiNGTSettings.ini"),
                     Path.Combine(flingPath, "TrainerSettings.ini")
                 })
        {
            if (!File.Exists(settingsFile))
            {
                continue;
            }

            var lines = await File.ReadAllLinesAsync(settingsFile);
            for (var index = 0; index < lines.Length; index++)
            {
                if (!lines[index].TrimStart().StartsWith("OnLoadMusic", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                lines[index] = Path.GetFileName(settingsFile).Equals("FLiNGTSettings.ini", StringComparison.OrdinalIgnoreCase)
                    ? $"OnLoadMusic = {!removeBackgroundMusic}"
                    : $"OnLoadMusic={!removeBackgroundMusic}";
            }

            await File.WriteAllLinesAsync(settingsFile, lines);
        }
    }

    private async Task RemoveFlingBackgroundMusicAsync(string sourceExe)
    {
        foreach (var resourceType in new[] { "MID", "MIDI" })
        {
            var logPath = Path.Combine(AppPaths.DownloadTempDirectory, "rh.log");
            var deleteResult = await _processService.RunAsync(
                AppPaths.ResourceHackerPath,
                ["-open", sourceExe, "-save", sourceExe, "-action", "delete", "-mask", $"{resourceType},,", "-log", logPath]);
            if (deleteResult.ExitCode != 0 || !File.Exists(logPath))
            {
                continue;
            }

            var logContent = await File.ReadAllTextAsync(logPath);
            var match = Regex.Match(logContent, @"Deleted:\s*(\w+),(\d+),(\d+)");
            if (!match.Success)
            {
                continue;
            }

            var resourceMask = $"{resourceType},{match.Groups[2].Value},{match.Groups[3].Value}";
            _ = await _processService.RunAsync(
                AppPaths.ResourceHackerPath,
                ["-open", sourceExe, "-save", sourceExe, "-action", "addoverwrite", "-res", AppPaths.EmptyMidiPath, "-mask", resourceMask]);
            break;
        }
    }

    private async Task<bool> HandleXiaoXingSpecialCasesAsync(
        TrainerCatalogEntry trainer,
        string displayName,
        string downloadRoot,
        string extractDirectory,
        ICollection<MoveItem> moveList)
    {
        var contents = Directory.EnumerateFileSystemEntries(extractDirectory).ToList();
        var hasExe = contents.Any(path => File.Exists(path) && Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase));
        var onlyFolders = contents.Count > 0 && contents.All(Directory.Exists);
        if (!hasExe && onlyFolders)
        {
            foreach (var folder in contents)
            {
                moveList.Add(new MoveItem(folder, Path.Combine(downloadRoot, $"{displayName} {Path.GetFileName(folder)}")));
            }

            return true;
        }

        var rarFile = contents.FirstOrDefault(path => File.Exists(path) && Path.GetExtension(path).Equals(".rar", StringComparison.OrdinalIgnoreCase));
        if (!hasExe && rarFile is not null)
        {
            var (exitCode, _, stderr) = await _processService.RunAsync(
                AppPaths.SevenZipPath,
                ["x", "-y", "-p123", rarFile, $"-o{extractDirectory}"]);
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"An error occurred while extracting downloaded trainer: {stderr}");
            }

            File.Delete(rarFile);
            moveList.Add(new MoveItem(extractDirectory, Path.Combine(downloadRoot, displayName)));
            return true;
        }

        return false;
    }

    private static bool HandleMultiVersionArchive(
        string extractDirectory,
        string displayName,
        string downloadRoot,
        ICollection<MoveItem> moveList)
    {
        var contents = Directory.EnumerateFileSystemEntries(extractDirectory).ToList();
        if (Directory.Exists(Path.Combine(extractDirectory, "gcm-instructions")))
        {
            return false;
        }

        var hasExecutableInRoot = contents.Any(path =>
            File.Exists(path) &&
            (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
             path.EndsWith(".ct", StringComparison.OrdinalIgnoreCase) ||
             path.EndsWith(".cetrainer", StringComparison.OrdinalIgnoreCase) ||
             path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)));

        var folders = contents.Where(Directory.Exists).ToList();
        if (hasExecutableInRoot || folders.Count == 0)
        {
            return false;
        }

        foreach (var folder in folders)
        {
            moveList.Add(new MoveItem(
                folder,
                Path.Combine(downloadRoot, $"{displayName} {StringUtilities.SymbolReplacement(Path.GetFileName(folder))}")));
        }

        return true;
    }

    private async Task UnlockXiaoXingAsync(TrainerCatalogEntry trainer, IEnumerable<MoveItem> moveList)
    {
        var patches = GetXiaoXingPatches(trainer.GameName);
        if (patches.Count == 0)
        {
            return;
        }

        foreach (var moveItem in moveList)
        {
            var sourceDirectory = Directory.Exists(moveItem.Source)
                ? moveItem.Source
                : Path.GetDirectoryName(moveItem.Source);
            if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
            {
                continue;
            }

            var exePath = Directory.EnumerateFiles(sourceDirectory)
                .FirstOrDefault(path =>
                    Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase) &&
                    !ExeExclusions.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase));
            if (exePath is null)
            {
                continue;
            }

            var inputFile = exePath + ".patch_in";
            var outputFile = exePath + ".patch_out";
            var backupFile = exePath + ".bak";
            File.Copy(exePath, backupFile, overwrite: true);
            File.Copy(exePath, inputFile, overwrite: true);

            var patchSucceeded = true;
            foreach (var patch in patches)
            {
                var (searchHex, searchMask) = ProcessPatternToHexAndMask(patch.Search);
                var (replaceHex, replaceMask) = ProcessPatternToHexAndMask(patch.Replace);
                var result = await _processService.RunAsync(
                    AppPaths.BinmayPath,
                    ["-i", inputFile, "-o", outputFile, "-s", searchHex, "-S", searchMask, "-r", replaceHex, "-R", replaceMask]);
                if (result.ExitCode != 0)
                {
                    patchSucceeded = false;
                    break;
                }

                File.Delete(inputFile);
                File.Move(outputFile, inputFile);
            }

            if (patchSucceeded && File.Exists(inputFile))
            {
                File.Delete(exePath);
                File.Move(inputFile, exePath);
            }
            else
            {
                File.Copy(backupFile, exePath, overwrite: true);
                if (File.Exists(inputFile))
                {
                    File.Delete(inputFile);
                }
            }
        }
    }

    private static List<BinaryPatch> GetXiaoXingPatches(string gameName) =>
        gameName switch
        {
            "Cyberpunk 2077" =>
            [
                new("833D????????000F84????????833D????????000F84????????", "833D????????00909090909090833D????????00909090909090"),
                new("833D????????000F84????????BA2E", "833D????????00909090909090BA2E")
            ],
            "Final Fantasy XV" or "Ho Tu Lo Shu The Books of Dragon" or "Xuan-Yuan Sword VII" =>
            [
                new("E8????????833D????????000F84????????BA2E040000", "??????????90909090909090909090909090??????????")
            ],
            "GuLong" or "Palworld" or "Baldur's Gate 3" or "Starfield" or "Hogwarts Legacy" or "Sword and Fairy 7" or "Path Of Wuxia" or "Elden Ring" =>
            [
                new("8B??E8??????00833D??????00000F84????0000", "8B??E8??????00833D??????0000909090909090")
            ],
            _ => []
        };

    private static (string Hex, string Mask) ProcessPatternToHexAndMask(string pattern)
    {
        var hexBytes = new List<string>();
        var maskBytes = new List<string>();
        for (var index = 0; index < pattern.Length; index += 2)
        {
            var token = pattern.Substring(index, 2);
            if (token == "??")
            {
                hexBytes.Add("00");
                maskBytes.Add("00");
            }
            else
            {
                hexBytes.Add(token);
                maskBytes.Add("ff");
            }
        }

        return (string.Join(" ", hexBytes), string.Join(" ", maskBytes));
    }

    private async Task WriteGcmInfoAsync(string destinationDirectory, TrainerCatalogEntry trainer, CancellationToken cancellationToken)
    {
        var info = new Dictionary<string, string>
        {
            ["game_name"] = trainer.GameName,
            ["origin"] = trainer.Origin
        };

        if (!string.IsNullOrWhiteSpace(trainer.Version))
        {
            info["version"] = trainer.Version;
        }

        if (trainer.Origin is "other" or "ct_other")
        {
            info["gcm_url"] = trainer.Url;
        }

        if (!string.IsNullOrWhiteSpace(trainer.Extension))
        {
            info["extension"] = trainer.Extension;
        }

        var json = JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(destinationDirectory, "gcm_info.json"), json, cancellationToken);
    }

    private sealed record MoveItem(string Source, string Destination);

    private sealed record BinaryPatch(string Search, string Replace);
}
