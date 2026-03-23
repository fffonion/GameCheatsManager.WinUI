using System.Diagnostics;
using System.Text.RegularExpressions;
using GameCheatsManager.WinUI.Models;
using Microsoft.Win32;

namespace GameCheatsManager.WinUI.Services;

public sealed class CustomizationService
{
    private readonly FileDownloadService _downloadService;
    private readonly ProcessService _processService;

    public CustomizationService(FileDownloadService downloadService, ProcessService processService)
    {
        _downloadService = downloadService;
        _processService = processService;
    }

    public void SetLaunchOnStartup(bool enabled)
    {
        const string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true) ?? Registry.CurrentUser.CreateSubKey(keyPath);
        if (enabled)
        {
            var executablePath = Environment.ProcessPath ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(executablePath))
            {
                key?.SetValue("Game Cheats Manager", executablePath);
            }
        }
        else
        {
            key?.DeleteValue("Game Cheats Manager", throwOnMissingValue: false);
        }
    }

    public async Task<bool> AddWindowsDefenderWhitelistAsync(IEnumerable<string> paths)
    {
        var pathList = paths.Where(static path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (pathList.Count == 0 || !File.Exists(AppPaths.ElevatePath))
        {
            return false;
        }

        var args = new List<string> { "whitelist" };
        args.AddRange(pathList);
        var result = await _processService.RunAsync(AppPaths.ElevatePath, args, createNoWindow: false);
        return result.ExitCode == 0;
    }

    public async Task<bool> AddCheatEngineTranslationAsync(string cheatEnginePath)
    {
        if (!Directory.Exists(cheatEnginePath) || !Directory.Exists(AppPaths.CheatEngineTranslationPath) || !File.Exists(AppPaths.ElevatePath))
        {
            return false;
        }

        var destination = Path.Combine(cheatEnginePath, "languages", "zh_CN");
        var result = await _processService.RunAsync(
            AppPaths.ElevatePath,
            ["copy", AppPaths.CheatEngineTranslationPath, destination],
            createNoWindow: false);
        return result.ExitCode == 0;
    }

    public async Task<bool> ApplyCheatEvolutionPatchAsync(string cheatEvolutionPath)
    {
        if (!Directory.Exists(cheatEvolutionPath) || !File.Exists(AppPaths.CheatEvolutionPatchedPath) || !File.Exists(AppPaths.ElevatePath))
        {
            return false;
        }

        var destination = Path.Combine(cheatEvolutionPath, "CheatEvolution_patched.exe");
        var result = await _processService.RunAsync(
            AppPaths.ElevatePath,
            ["copy", AppPaths.CheatEvolutionPatchedPath, destination],
            createNoWindow: false);
        return result.ExitCode == 0;
    }

    public IReadOnlyList<string> FindWeModVersions(string installPath)
    {
        if (!Directory.Exists(installPath))
        {
            return [];
        }

        return Directory.EnumerateDirectories(installPath, "app-*")
            .Select(Path.GetFileName)
            .Where(static name => name is not null)
            .Select(static name => name!["app-".Length..])
            .OrderByDescending(static version => version, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> ApplyWeModCustomizationAsync(
        string installPath,
        IReadOnlyList<string> installedVersions,
        string selectedVersion,
        string patchMethod,
        bool enablePro,
        bool disableAutoUpdate,
        bool deleteOtherVersions,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<string>();
        var selectedPath = Path.Combine(installPath, $"app-{selectedVersion}");
        if (!Directory.Exists(selectedPath))
        {
            messages.Add("Selected Wand version was not found.");
            return messages;
        }

        var weModExe = File.Exists(Path.Combine(selectedPath, "Wand.exe"))
            ? Path.Combine(selectedPath, "Wand.exe")
            : Path.Combine(selectedPath, "WeMod.exe");
        var weModExeName = Path.GetFileName(weModExe);
        if (Process.GetProcessesByName(Path.GetFileNameWithoutExtension(weModExe)).Length > 0)
        {
            messages.Add("Wand is currently running. Close it before applying patches.");
            return messages;
        }

        var asarPath = Path.Combine(selectedPath, "resources", "app.asar");
        var asarCopy = Path.Combine(AppPaths.WeModTempDirectory, "app.asar");
        var asarBackup = asarPath + ".bak";
        Directory.CreateDirectory(AppPaths.WeModTempDirectory);

        if (enablePro)
        {
            if (!await PatchWeModExecutableAsync(weModExe))
            {
                messages.Add("Failed to patch the Wand executable.");
            }
            else
            {
                File.Copy(asarPath, asarCopy, overwrite: true);
                var extracted = await _processService.RunAsync(
                    AppPaths.SevenZipPath,
                    ["e", "-y", asarCopy, "app*bundle.js", "index.js", $"-o{AppPaths.WeModTempDirectory}"]);
                if (extracted.ExitCode != 0)
                {
                    messages.Add("Failed to extract Wand app resources.");
                }
                else
                {
                    var patterns = await _downloadService.GetPatchPatternsAsync(patchMethod, enableDev: false, cancellationToken);
                    if (patterns is null || patterns.Count == 0)
                    {
                        messages.Add("Patch patterns endpoint is not configured.");
                    }
                    else if (!PatchJavaScriptFiles(patterns))
                    {
                        messages.Add("Failed to patch Wand JavaScript bundles.");
                    }
                    else
                    {
                        File.Copy(asarPath, asarBackup, overwrite: true);
                        var repack = await _processService.RunAsync(
                            AppPaths.SevenZipPath,
                            ["a", "-y", asarCopy, Path.Combine(AppPaths.WeModTempDirectory, "*.js")]);
                        if (repack.ExitCode != 0)
                        {
                            messages.Add("Failed to rebuild Wand resources.");
                        }
                        else
                        {
                            File.Copy(asarCopy, asarPath, overwrite: true);
                            messages.Add("Wand Pro activated.");
                        }
                    }
                }
            }
        }
        else if (File.Exists(asarBackup))
        {
            File.Copy(asarBackup, asarPath, overwrite: true);
            messages.Add("Wand Pro disabled.");
        }

        var updateExe = Path.Combine(installPath, "Update.exe");
        var updateExeBackup = updateExe + ".bak";
        try
        {
            if (disableAutoUpdate && File.Exists(updateExe))
            {
                File.Move(updateExe, updateExeBackup, overwrite: true);
                messages.Add("Wand auto update disabled.");
            }
            else if (!disableAutoUpdate && File.Exists(updateExeBackup))
            {
                File.Move(updateExeBackup, updateExe, overwrite: true);
                messages.Add("Wand auto update enabled.");
            }
        }
        catch (Exception ex)
        {
            messages.Add($"Failed to update Wand updater state: {ex.Message}");
        }

        if (deleteOtherVersions)
        {
            foreach (var version in installedVersions.Where(version => !string.Equals(version, selectedVersion, StringComparison.OrdinalIgnoreCase)))
            {
                var folder = Path.Combine(installPath, $"app-{version}");
                try
                {
                    if (Directory.Exists(folder))
                    {
                        Directory.Delete(folder, recursive: true);
                        messages.Add($"Deleted Wand version: {version}");
                    }
                }
                catch (Exception ex)
                {
                    messages.Add($"Failed to delete Wand version {version}: {ex.Message}");
                }
            }
        }

        try
        {
            Directory.Delete(AppPaths.WeModTempDirectory, recursive: true);
        }
        catch
        {
        }

        return messages;
    }

    private async Task<bool> PatchWeModExecutableAsync(string executablePath)
    {
        var backupPath = executablePath + ".bak";
        File.Copy(executablePath, backupPath, overwrite: true);
        var result = await _processService.RunAsync(
            AppPaths.BinmayPath,
            ["-i", backupPath, "-o", executablePath, "-s", "t:00001101", "-r", "t:00000101"]);
        File.Delete(backupPath);
        return result.ExitCode == 0;
    }

    private static bool PatchJavaScriptFiles(IReadOnlyDictionary<string, string> patterns)
    {
        var files = Directory.EnumerateFiles(AppPaths.WeModTempDirectory, "*.js").ToList();
        foreach (var (pattern, replacement) in patterns)
        {
            var matchingFile = files.FirstOrDefault(file => Regex.IsMatch(File.ReadAllText(file), pattern));
            if (matchingFile is null)
            {
                return false;
            }

            var content = File.ReadAllText(matchingFile);
            var updated = Regex.Replace(content, pattern, replacement);
            File.WriteAllText(matchingFile, updated);
        }

        return true;
    }
}
