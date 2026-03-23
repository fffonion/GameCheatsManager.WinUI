namespace GameCheatsManager.WinUI.Services;

public static class AppPaths
{
    public static string AppRoot { get; } = ResolveAppRoot();

    public static string DependenciesRoot { get; } = Path.Combine(AppRoot, "Dependencies");

    public static string SettingsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GCM Settings");

    public static string DatabaseDirectory { get; } = Path.Combine(SettingsDirectory, "db");

    public static string SettingsFile { get; } = Path.Combine(SettingsDirectory, "settings.json");

    public static string DownloadTempDirectory { get; } = Path.Combine(Path.GetTempPath(), "GameCheatsManagerTemp", "download");

    public static string VersionTempDirectory { get; } = Path.Combine(Path.GetTempPath(), "GameCheatsManagerTemp", "version");

    public static string WeModTempDirectory { get; } = Path.Combine(Path.GetTempPath(), "GameCheatsManagerTemp", "wemod");

    public static string SevenZipPath { get; } = Path.Combine(DependenciesRoot, "7z", "7z.exe");

    public static string ResourceHackerPath { get; } = Path.Combine(DependenciesRoot, "ResourceHacker.exe");

    public static string BinmayPath { get; } = Path.Combine(DependenciesRoot, "binmay.exe");

    public static string ElevatePath { get; } = Path.Combine(DependenciesRoot, "Elevate.exe");

    public static string EmptyMidiPath { get; } = Path.Combine(DependenciesRoot, "TrainerBGM.mid");

    public static string CheatEvolutionPatchedPath { get; } = Path.Combine(DependenciesRoot, "CheatEvolution_patched.exe");

    public static string CheatEngineTranslationPath { get; } = Path.Combine(DependenciesRoot, "CE Translations", "zh_CN");

    static AppPaths()
    {
        Directory.CreateDirectory(SettingsDirectory);
        Directory.CreateDirectory(DatabaseDirectory);
    }

    public static string GetDefaultDownloadPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GCM Trainers");

    public static string GetDefaultWeModPath()
    {
        var wand = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Wand");
        return Directory.Exists(wand)
            ? wand
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WeMod");
    }

    public static string GetDefaultCheatEnginePath()
    {
        const string basePath = @"C:\Program Files";
        if (!Directory.Exists(basePath))
        {
            return string.Empty;
        }

        var bestVersion = Array.Empty<int>();
        var bestPath = string.Empty;
        foreach (var directory in Directory.EnumerateDirectories(basePath, "Cheat Engine*"))
        {
            var folderName = Path.GetFileName(directory);
            var match = System.Text.RegularExpressions.Regex.Match(folderName, @"Cheat Engine (\d+(?:\.\d+)*)");
            if (!match.Success)
            {
                bestPath = directory;
                continue;
            }

            var parts = match.Groups[1].Value.Split('.').Select(static part => int.TryParse(part, out var value) ? value : 0).ToArray();
            Array.Resize(ref parts, 3);
            if (CompareVersions(parts, bestVersion) > 0)
            {
                bestVersion = parts;
                bestPath = directory;
            }
        }

        return bestPath;
    }

    private static int CompareVersions(int[] left, int[] right)
    {
        var count = Math.Max(left.Length, right.Length);
        for (var index = 0; index < count; index++)
        {
            var leftValue = index < left.Length ? left[index] : 0;
            var rightValue = index < right.Length ? right[index] : 0;
            if (leftValue != rightValue)
            {
                return leftValue.CompareTo(rightValue);
            }
        }

        return 0;
    }

    private static string ResolveAppRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "GameCheatsManager.WinUI.csproj")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "Dependencies")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
