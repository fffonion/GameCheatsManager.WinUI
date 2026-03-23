using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using GameCheatsManager.WinUI.Models;
using GameCheatsManager.WinUI.Services;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace GameCheatsManager.WinUI;

public sealed partial class MainWindow : Window
{
    private const string AppVersion = "2.5.0-beta.3";
    private const string WebsiteLink = "https://gamezonelabs.com";
    private const string AllTrainersLink = "https://gamezonelabs.com/products/game-cheats-manager/trainers";
    private const string GithubLink = "https://github.com/fffonion/GameCheatsManager.WinUI";
    private const string GithubReleasesLink = "https://github.com/fffonion/GameCheatsManager.WinUI/releases";
    private const string BilibiliLink = "https://space.bilibili.com/256673766";

    private readonly SettingsService _settingsService = new();
    private readonly BackendConfigService _backendConfigService = new();
    private readonly ProcessService _processService = new();
    private readonly LocalizationService _localizationService = new();
    private readonly TrainerLibraryService _trainerLibraryService;

    private readonly BackendConfig _backendConfig;
    private readonly FileDownloadService _fileDownloadService;
    private readonly TrainerCatalogService _trainerCatalogService;
    private readonly DownloadService _downloadService;
    private readonly CustomizationService _customizationService;
    private readonly UpdateService _updateService;

    private readonly ObservableCollection<InstalledTrainer> _installedTrainerItems = [];
    private readonly ObservableCollection<TrainerCatalogEntry> _downloadResultItems = [];
    private readonly Queue<TrainerCatalogEntry> _downloadQueue = new();
    private readonly HashSet<string> _runningStatusKeys = [];

    private AppSettings _settings = new();
    private DispatcherQueueTimer? _intervalTimer;
    private bool _initialized;
    private bool _isSearching;
    private bool _isDownloading;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureWindow();

        _backendConfig = _backendConfigService.Load();
        _fileDownloadService = new FileDownloadService(_backendConfig);
        _trainerCatalogService = new TrainerCatalogService(_fileDownloadService);
        _trainerLibraryService = new TrainerLibraryService(_processService);
        _downloadService = new DownloadService(_fileDownloadService, _processService, _trainerCatalogService);
        _customizationService = new CustomizationService(_fileDownloadService, _processService);
        _updateService = new UpdateService(_fileDownloadService);

        InstalledTrainersListView.ItemsSource = _installedTrainerItems;
        DownloadResultsListView.ItemsSource = _downloadResultItems;

        RootGrid.Loaded += OnRootLoaded;
        Closed += OnWindowClosed;
    }

    private async void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await InitializeAsync();
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _trainerLibraryService.CleanupLaunchJunctions();
    }

    private async Task InitializeAsync()
    {
        _settings = await _settingsService.LoadAsync();
        await ApplyBackendFallbacksAsync();
        ApplyTheme();
        ApplyLocalization();
        DownloadPathTextBox.Text = _settings.DownloadPath;
        await RefreshInstalledTrainersAsync();

        AddStatusLine(Tr(_backendConfig.HasSignedDownloadConfig
            ? "Backend config detected: signed download available."
            : "Backend config detected: signed download missing."));

        await EnsureInitialCatalogDataAsync();

        await ShowWarningIfNeededAsync();
        await ShowAnnouncementIfNeededAsync();

        if (_settings.CheckAppUpdate)
        {
            _ = CheckForAppUpdateAsync();
        }

        _ = RunAutoUpdateCycleAsync();

        _intervalTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _intervalTimer.Interval = TimeSpan.FromHours(1);
        _intervalTimer.Tick += async (_, _) => await RunAutoUpdateCycleAsync();
        _intervalTimer.Start();
    }

    private void ApplyTheme()
    {
        RootGrid.RequestedTheme = _settings.Theme == "light" ? ElementTheme.Light : ElementTheme.Dark;
    }

    private void ConfigureWindow()
    {
        const int preferredWidth = 2080;
        const int preferredHeight = 1230;

        var windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        var width = Math.Min(preferredWidth, Math.Max(1200, displayArea.WorkArea.Width - 40));
        var height = Math.Min(preferredHeight, Math.Max(900, displayArea.WorkArea.Height - 40));

        appWindow.Resize(new SizeInt32(width, height));

        var centeredX = displayArea.WorkArea.X + Math.Max(0, (displayArea.WorkArea.Width - width) / 2);
        var centeredY = displayArea.WorkArea.Y + Math.Max(0, (displayArea.WorkArea.Height - height) / 2);
        appWindow.Move(new PointInt32(centeredX, centeredY));
    }

    private void ApplyLocalization()
    {
        Title = Tr("Game Cheats Manager");
        OptionsMenuBarItem.Title = Tr("Options");
        SettingsMenuFlyoutItem.Text = Tr("Settings");
        ImportMenuFlyoutItem.Text = Tr("Import Trainers");
        OpenDownloadPathMenuFlyoutItem.Text = Tr("Open Trainer Download Path");
        WhitelistMenuFlyoutItem.Text = Tr("Add Paths to Whitelist");
        AboutMenuFlyoutItem.Text = Tr("About");
        DataUpdateMenuBarItem.Title = Tr("Data Update");
        UpdateTranslationsMenuFlyoutItem.Text = Tr("Update Translation Data");
        UpdateSearchDataMenuFlyoutItem.Text = Tr("Update Trainer Search Data");
        UpdateTrainersMenuFlyoutItem.Text = Tr("Update Trainers");
        TrainerManagementMenuBarItem.Title = Tr("Trainer Management");
        TrainerManagementMenuFlyoutItem.Text = Tr("Open Trainer Management");
        UploadTrainerMenuBarItem.Title = Tr("Upload Trainer");
        UploadTrainerMenuFlyoutItem.Text = Tr("Open Upload Trainer");
        BrowseAllTrainersMenuBarItem.Title = Tr("Browse All Trainers");
        BrowseAllTrainersMenuFlyoutItem.Text = Tr("Open Trainers Page");

        InstalledTrainersHeaderTextBlock.Text = Tr("Installed Trainers");
        InstalledSearchBox.PlaceholderText = Tr("Search for installed trainers");
        LaunchButton.Content = Tr("Launch");
        DeleteButton.Content = Tr("Delete");
        DownloadTrainersHeaderTextBlock.Text = Tr("Download Trainers");
        DownloadSearchBox.PlaceholderText = Tr("Enter keywords to download trainers");
        SearchButton.Content = Tr("Search");
        if (string.IsNullOrWhiteSpace(ActiveOperationTextBlock.Text) || ActiveOperationTextBlock.Text == "Idle" || ActiveOperationTextBlock.Text == Tr("Idle"))
        {
            ActiveOperationTextBlock.Text = Tr("Idle");
        }

        DownloadPathLabelTextBlock.Text = Tr("Trainer download path:");
        BrowseButton.Content = "...";
        ToolTipService.SetToolTip(BrowseButton, Tr("Browse"));
        ActivityHeaderTextBlock.Text = Tr("Activity");
    }

    private string Tr(string text) => _localizationService.Translate(text, _settings.Language);

    private string Trf(string text, params object[] args) => string.Format(CultureInfo.CurrentCulture, Tr(text), args);

    private string LocalizeUserText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        const string updatingPrefix = "Updating ";
        if (text.StartsWith(updatingPrefix, StringComparison.Ordinal) && text.EndsWith("...", StringComparison.Ordinal))
        {
            var name = text.Substring(updatingPrefix.Length, text.Length - updatingPrefix.Length - 3);
            return Trf("Updating {0}...", name);
        }

        const string unsupportedTrainerOriginPrefix = "Unsupported trainer origin: ";
        if (text.StartsWith(unsupportedTrainerOriginPrefix, StringComparison.Ordinal))
        {
            return Trf("Unsupported trainer origin: {0}", text.Substring(unsupportedTrainerOriginPrefix.Length));
        }

        const string movingTrainerErrorPrefix = "An error occurred when moving trainer: ";
        if (text.StartsWith(movingTrainerErrorPrefix, StringComparison.Ordinal))
        {
            return Trf("An error occurred when moving trainer: {0}", text.Substring(movingTrainerErrorPrefix.Length));
        }

        const string extractArchiveErrorPrefix = "An error occurred while extracting archive: ";
        if (text.StartsWith(extractArchiveErrorPrefix, StringComparison.Ordinal))
        {
            return Trf("An error occurred while extracting archive: {0}", text.Substring(extractArchiveErrorPrefix.Length));
        }

        const string extractDownloadedTrainerErrorPrefix = "An error occurred while extracting downloaded trainer: ";
        if (text.StartsWith(extractDownloadedTrainerErrorPrefix, StringComparison.Ordinal))
        {
            return Trf("An error occurred while extracting downloaded trainer: {0}", text.Substring(extractDownloadedTrainerErrorPrefix.Length));
        }

        const string wandUpdaterStateErrorPrefix = "Failed to update Wand updater state: ";
        if (text.StartsWith(wandUpdaterStateErrorPrefix, StringComparison.Ordinal))
        {
            return Trf("Failed to update Wand updater state: {0}", text.Substring(wandUpdaterStateErrorPrefix.Length));
        }

        const string deletedWandVersionPrefix = "Deleted Wand version: ";
        if (text.StartsWith(deletedWandVersionPrefix, StringComparison.Ordinal))
        {
            return Trf("Deleted Wand version: {0}", text.Substring(deletedWandVersionPrefix.Length));
        }

        return Tr(text);
    }

    private async Task ApplyBackendFallbacksAsync()
    {
        if (_backendConfig.HasSignedDownloadConfig)
        {
            return;
        }

        var changed = false;
        if (!string.Equals(_settings.FlingDownloadServer, "official", StringComparison.OrdinalIgnoreCase))
        {
            _settings.FlingDownloadServer = "official";
            changed = true;
        }

        if (changed)
        {
            await _settingsService.SaveAsync(_settings);
        }

        ShowNotification(
            Tr("Warning"),
            Tr("Private backend is not configured. Switched FLiNG to official mode; GCM/XiaoXing/CT signed downloads are unavailable."),
            InfoBarSeverity.Warning);
    }

    private async Task EnsureInitialCatalogDataAsync()
    {
        if (string.Equals(_settings.FlingDownloadServer, "official", StringComparison.OrdinalIgnoreCase))
        {
            var archiveExists = File.Exists(Path.Combine(AppPaths.DatabaseDirectory, "fling_archive.html"));
            var mainExists = File.Exists(Path.Combine(AppPaths.DatabaseDirectory, "fling_main.html"));
            if (!archiveExists || !mainExists)
            {
                AddStatusLine(Tr("Updating FLiNG data..."));
                var success = await _trainerCatalogService.FetchFlingDataAsync(_settings);
                AddStatusLine(Tr(success ? "FLiNG data updated." : "FLiNG data update failed."), success ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
            }
        }
    }

    private async Task RefreshInstalledTrainersAsync()
    {
        var installed = _trainerLibraryService.GetInstalledTrainers(_settings.DownloadPath, _settings.Language);
        var filtered = _trainerLibraryService.FilterInstalledTrainers(installed, InstalledSearchBox.Text);

        _installedTrainerItems.Clear();
        foreach (var trainer in filtered)
        {
            _installedTrainerItems.Add(trainer);
        }

        await Task.CompletedTask;
    }

    private void AddStatusLine(string message, InfoBarSeverity severity = InfoBarSeverity.Informational)
    {
        var brush = severity switch
        {
            InfoBarSeverity.Success => new SolidColorBrush(Microsoft.UI.Colors.ForestGreen),
            InfoBarSeverity.Warning => new SolidColorBrush(Microsoft.UI.Colors.DarkOrange),
            InfoBarSeverity.Error => new SolidColorBrush(Microsoft.UI.Colors.IndianRed),
            _ => (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        };

        var textBlock = new TextBlock
        {
            Text = $"[{DateTime.Now:HH:mm:ss}] {message}",
            TextWrapping = TextWrapping.Wrap,
            Foreground = brush
        };

        StatusPanel.Children.Insert(0, textBlock);
        while (StatusPanel.Children.Count > 40)
        {
            StatusPanel.Children.RemoveAt(StatusPanel.Children.Count - 1);
        }
    }

    private void ShowNotification(string title, string message, InfoBarSeverity severity)
    {
        NotificationBar.Title = title;
        NotificationBar.Message = message;
        NotificationBar.Severity = severity;
        NotificationBar.IsOpen = true;
    }

    private void UpdateOperationStatus(string text)
    {
        ActiveOperationTextBlock.Text = text;
    }

    private void ResetProgress()
    {
        DownloadProgressBar.IsIndeterminate = false;
        DownloadProgressBar.Value = 0;
        DownloadProgressBar.Visibility = Visibility.Collapsed;
        DownloadProgressTextBlock.Text = string.Empty;
    }

    private void UpdateProgress(DownloadProgressInfo progressInfo)
    {
        DownloadProgressBar.Visibility = Visibility.Visible;
        if (progressInfo.TotalBytes <= 0)
        {
            DownloadProgressBar.IsIndeterminate = true;
            DownloadProgressTextBlock.Text = FormatSize(progressInfo.DownloadedBytes);
            return;
        }

        DownloadProgressBar.IsIndeterminate = false;
        DownloadProgressBar.Value = (double)progressInfo.DownloadedBytes / progressInfo.TotalBytes * 100d;
        DownloadProgressTextBlock.Text = $"{FormatSize(progressInfo.DownloadedBytes)} / {FormatSize(progressInfo.TotalBytes)}";
    }

    private static string FormatSize(long size)
    {
        double value = size;
        foreach (var unit in new[] { "B", "KB", "MB", "GB", "TB" })
        {
            if (value < 1024 || unit == "TB")
            {
                return unit == "B" ? $"{value:0} {unit}" : $"{value:0.0} {unit}";
            }

            value /= 1024;
        }

        return $"{size} B";
    }

    private ContentDialog CreateDialog(string title, UIElement content, string? primaryButtonText = null, string? closeButtonText = null)
    {
        return new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = primaryButtonText ?? string.Empty,
            CloseButtonText = closeButtonText ?? Tr("Close"),
            DefaultButton = string.IsNullOrWhiteSpace(primaryButtonText) ? ContentDialogButton.Close : ContentDialogButton.Primary,
            XamlRoot = RootGrid.XamlRoot
        };
    }

    private async Task<bool> ConfirmAsync(string title, string message, string primaryText, string closeText)
    {
        var dialog = CreateDialog(title, new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, primaryText, closeText);
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<StorageFile[]?> PickFilesAsync(params string[] extensions)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        foreach (var extension in extensions)
        {
            picker.FileTypeFilter.Add(extension);
        }

        var files = await picker.PickMultipleFilesAsync();
        return files?.ToArray();
    }

    private async Task<StorageFile?> PickSingleFileAsync(params string[] extensions)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        foreach (var extension in extensions)
        {
            picker.FileTypeFilter.Add(extension);
        }

        return await picker.PickSingleFileAsync();
    }

    private async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.FileTypeFilter.Add("*");
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    private async Task SearchAsync()
    {
        if (_isSearching || string.IsNullOrWhiteSpace(DownloadSearchBox.Text))
        {
            return;
        }

        _isSearching = true;
        _downloadResultItems.Clear();
        UpdateOperationStatus(Tr("Search"));
        ResetProgress();

        try
        {
            var results = await _trainerCatalogService.SearchAsync(DownloadSearchBox.Text.Trim(), _settings);
            foreach (var result in results)
            {
                _downloadResultItems.Add(result);
            }

            if (results.Count == 0)
            {
                ShowNotification(Tr("Search"), Tr("No search results found."), InfoBarSeverity.Warning);
                AddStatusLine(Tr("No search results found."), InfoBarSeverity.Warning);
            }
            else
            {
                AddStatusLine(Trf("Search returned {0} trainer(s).", results.Count), InfoBarSeverity.Success);
            }
        }
        catch (Exception ex)
        {
            ShowNotification(Tr("Search Failed"), ex.Message, InfoBarSeverity.Error);
            AddStatusLine(Trf("Search failed: {0}", ex.Message), InfoBarSeverity.Error);
        }
        finally
        {
            _isSearching = false;
            UpdateOperationStatus(Tr("Idle"));
        }
    }

    private async Task QueueDownloadAsync(TrainerCatalogEntry trainer)
    {
        _downloadQueue.Enqueue(trainer);
        if (_isDownloading)
        {
            return;
        }

        _isDownloading = true;
        while (_downloadQueue.Count > 0)
        {
            var next = _downloadQueue.Dequeue();
            UpdateOperationStatus(!string.IsNullOrWhiteSpace(next.TrainerDirectory) ? Trf("Updating {0}...", next.DisplayName) : Tr("Download"));
            ResetProgress();

            var result = await _downloadService.DownloadTrainerAsync(
                next,
                _installedTrainerItems.ToList(),
                _settings,
                new Progress<string>(text => UpdateOperationStatus(LocalizeUserText(text))),
                new Progress<DownloadProgressInfo>(UpdateProgress));
            var localizedMessage = LocalizeUserText(result.Message);

            if (result.Success)
            {
                AddStatusLine(localizedMessage, InfoBarSeverity.Success);
                ShowNotification(Tr("Download"), localizedMessage, InfoBarSeverity.Success);
                if (!string.IsNullOrWhiteSpace(result.InstructionDirectory) && Directory.Exists(result.InstructionDirectory))
                {
                    _processService.OpenWithShell(result.InstructionDirectory);
                }
            }
            else
            {
                AddStatusLine(localizedMessage, InfoBarSeverity.Error);
                ShowNotification(Tr("Download Failed"), localizedMessage, InfoBarSeverity.Error);
            }

            await RefreshInstalledTrainersAsync();
            ResetProgress();
        }

        _isDownloading = false;
        UpdateOperationStatus(Tr("Idle"));
    }

    private async Task RunStatusOperationAsync(string key, string startingMessage, Func<Task> action)
    {
        if (_runningStatusKeys.Contains(key))
        {
            return;
        }

        _runningStatusKeys.Add(key);
        AddStatusLine(startingMessage);
        try
        {
            await action();
        }
        finally
        {
            _runningStatusKeys.Remove(key);
        }
    }

    private async Task RunAutoUpdateCycleAsync()
    {
        if (_settings.AutoUpdateTranslations && _backendConfig.HasSignedDownloadConfig)
        {
            await RunStatusOperationAsync("translations", Tr("Updating translation data..."), async () =>
            {
                var success = await _trainerCatalogService.FetchTrainerTranslationsAsync();
                AddStatusLine(Tr(success ? "Translation data updated." : "Translation data update failed."), success ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
            });
        }

        if (_settings.AutoUpdateGCMData && _backendConfig.HasSignedDownloadConfig)
        {
            await RunStatusOperationAsync("gcm", Tr("Updating GCM data..."), async () =>
            {
                var success = await _trainerCatalogService.FetchGcmDataAsync();
                AddStatusLine(Tr(success ? "GCM data updated." : "GCM data update failed."), success ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
            });
        }

        if (_settings.AutoUpdateFlingData)
        {
            await RunStatusOperationAsync("fling", Tr("Updating FLiNG data..."), async () =>
            {
                var success = await _trainerCatalogService.FetchFlingDataAsync(_settings);
                AddStatusLine(Tr(success ? "FLiNG data updated." : "FLiNG data update failed."), success ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
            });
        }

        if (_settings.EnableXiaoXing && _settings.AutoUpdateXiaoXingData && _backendConfig.HasSignedDownloadConfig)
        {
            await RunStatusOperationAsync("xiaoxing", Tr("Updating XiaoXing data..."), async () =>
            {
                var success = await _trainerCatalogService.FetchXiaoXingDataAsync();
                AddStatusLine(Tr(success ? "XiaoXing data updated." : "XiaoXing data update failed."), success ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
            });
        }

        if (_settings.EnableCT && _settings.AutoUpdateCTData && _backendConfig.HasSignedDownloadConfig)
        {
            await RunStatusOperationAsync("ct", Tr("Updating Cheat Table data..."), async () =>
            {
                var success = await _trainerCatalogService.FetchCheatTableDataAsync();
                AddStatusLine(Tr(success ? "Cheat Table data updated." : "Cheat Table data update failed."), success ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
            });
        }

        if (_settings.AutoUpdateFlingTrainers ||
            (_backendConfig.HasSignedDownloadConfig &&
             (_settings.AutoUpdateGCMTrainers || _settings.AutoUpdateXiaoXingTrainers || _settings.AutoUpdateCTTrainers)))
        {
            await UpdateInstalledTrainersAsync(autoCheck: true);
        }
    }

    private async Task UpdateInstalledTrainersAsync(bool autoCheck)
    {
        if (_runningStatusKeys.Contains("trainer-update"))
        {
            return;
        }

        await RunStatusOperationAsync("trainer-update", Tr("Checking for trainer updates..."), async () =>
        {
            var updates = await _trainerCatalogService.CheckTrainerUpdatesAsync(_installedTrainerItems, _settings, autoCheck);
            if (updates.Count == 0)
            {
                if (!autoCheck)
                {
                    ShowNotification(Tr("Updates"), Tr("No trainer updates found."), InfoBarSeverity.Success);
                }

                AddStatusLine(Tr("No trainer updates found."));
                return;
            }

            AddStatusLine(Trf("Found {0} trainer update(s).", updates.Count), InfoBarSeverity.Success);
            foreach (var update in updates)
            {
                await QueueDownloadAsync(update);
            }
        });
    }

    private async Task CheckForAppUpdateAsync()
    {
        try
        {
            var latestVersion = await _updateService.GetLatestVersionAsync("GCM");
            if (!string.IsNullOrWhiteSpace(latestVersion) && UpdateService.IsNewerVersion(latestVersion, AppVersion))
            {
                var updateNow = await ConfirmAsync(
                    Tr("Update Available"),
                    Trf("New version found: {0} -> {1}\n\nOpen the release page now?", AppVersion, latestVersion),
                    Tr("Open"),
                    Tr("Later"));
                if (updateNow)
                {
                    _processService.OpenUrl(GithubReleasesLink);
                }
            }
        }
        catch (Exception ex)
        {
            AddStatusLine(Trf("App update check failed: {0}", ex.Message), InfoBarSeverity.Warning);
        }
    }

    private async Task ShowWarningIfNeededAsync()
    {
        if (!_settings.ShowWarning)
        {
            return;
        }

        var dontShow = new CheckBox { Content = Tr("Don't show again") };
        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = Tr("This software is open source and provided free of charge. Resale is strictly prohibited."),
                    TextWrapping = TextWrapping.Wrap
                },
                new HyperlinkButton { Content = WebsiteLink, NavigateUri = new Uri(WebsiteLink) },
                new HyperlinkButton { Content = GithubLink, NavigateUri = new Uri(GithubLink) },
                new HyperlinkButton { Content = BilibiliLink, NavigateUri = new Uri(BilibiliLink) },
                dontShow
            }
        };

        var dialog = CreateDialog(Tr("Warning"), content, closeButtonText: Tr("OK"));
        await dialog.ShowAsync();
        if (dontShow.IsChecked == true)
        {
            _settings.ShowWarning = false;
            await _settingsService.SaveAsync(_settings);
        }
    }

    private async Task ShowAnnouncementIfNeededAsync()
    {
        try
        {
            var announcement = await _updateService.FetchLatestAnnouncementAsync(_settings.LastSeenAnnouncementId);
            if (announcement is null)
            {
                return;
            }

            var useChinese = _settings.Language is "zh_CN" or "zh_TW";
            var title = useChinese ? announcement.TitleZh : announcement.TitleEn;
            var message = useChinese ? announcement.MessageZh : announcement.MessageEn;

            var dialog = CreateDialog(
                string.IsNullOrWhiteSpace(title) ? Tr("Announcement") : title,
                new ScrollViewer
                {
                    MaxHeight = 400,
                    Content = new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap
                    }
                },
                closeButtonText: Tr("OK"));
            await dialog.ShowAsync();

            _settings.LastSeenAnnouncementId = announcement.Id;
            await _settingsService.SaveAsync(_settings);
        }
        catch (Exception ex)
        {
            AddStatusLine(Trf("Announcement fetch failed: {0}", ex.Message), InfoBarSeverity.Warning);
        }
    }

    private async Task ShowSettingsDialogAsync()
    {
        var themeCombo = new ComboBox
        {
            ItemsSource = new[] { "dark", "light" },
            SelectedItem = _settings.Theme
        };
        var languageCombo = new ComboBox
        {
            ItemsSource = new[] { "en_US", "zh_CN", "zh_TW", "de_DE" },
            SelectedItem = _settings.Language
        };
        var launchOnStartup = new CheckBox { Content = Tr("Launch app on system startup"), IsChecked = _settings.LaunchAppOnStartup };
        var checkAppUpdate = new CheckBox { Content = Tr("Check for software updates at startup"), IsChecked = _settings.CheckAppUpdate };
        var safePath = new CheckBox { Content = Tr("Use safe launch path"), IsChecked = _settings.SafePath };
        var alwaysEnglish = new CheckBox { Content = Tr("Always show search results in English"), IsChecked = _settings.EnSearchResults };
        var sortByOrigin = new CheckBox { Content = Tr("Sort search results by origin"), IsChecked = _settings.SortByOrigin };
        var autoUpdateTranslations = new CheckBox { Content = Tr("Update trainer translations automatically"), IsChecked = _settings.AutoUpdateTranslations };

        var panel = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                LabeledControl(Tr("Theme"), themeCombo),
                LabeledControl(Tr("Language"), languageCombo),
                launchOnStartup,
                checkAppUpdate,
                safePath,
                alwaysEnglish,
                sortByOrigin,
                autoUpdateTranslations
            }
        };

        var dialog = CreateDialog(Tr("Settings"), panel, Tr("Apply"), Tr("Cancel"));
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var oldTheme = _settings.Theme;
        var oldLanguage = _settings.Language;
        _settings.Theme = themeCombo.SelectedItem?.ToString() ?? _settings.Theme;
        _settings.Language = languageCombo.SelectedItem?.ToString() ?? _settings.Language;
        _settings.LaunchAppOnStartup = launchOnStartup.IsChecked == true;
        _settings.CheckAppUpdate = checkAppUpdate.IsChecked == true;
        _settings.SafePath = safePath.IsChecked == true;
        _settings.EnSearchResults = alwaysEnglish.IsChecked == true;
        _settings.SortByOrigin = sortByOrigin.IsChecked == true;
        _settings.AutoUpdateTranslations = autoUpdateTranslations.IsChecked == true;

        _customizationService.SetLaunchOnStartup(_settings.LaunchAppOnStartup);
        await _settingsService.SaveAsync(_settings);
        ApplyTheme();
        ApplyLocalization();
        await RefreshInstalledTrainersAsync();

        if (!string.Equals(oldTheme, _settings.Theme, StringComparison.Ordinal) ||
            !string.Equals(oldLanguage, _settings.Language, StringComparison.Ordinal))
        {
            ShowNotification(Tr("Settings"), Tr("Theme and language changes are saved. Restart the app to fully apply them."), InfoBarSeverity.Warning);
        }
        else
        {
            ShowNotification(Tr("Settings"), Tr("Settings saved."), InfoBarSeverity.Success);
        }
    }

    private async Task ShowAboutDialogAsync()
    {
        var latestVersion = await _updateService.GetLatestVersionAsync("GCM") ?? Tr("Unavailable");
        var panel = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = Tr("Game Cheats Manager"), FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                new TextBlock { Text = $"{Tr("Current version:")} {AppVersion}" },
                new TextBlock { Text = $"{Tr("Newest version:")} {latestVersion}" },
                new HyperlinkButton { Content = WebsiteLink, NavigateUri = new Uri(WebsiteLink) },
                new HyperlinkButton { Content = GithubLink, NavigateUri = new Uri(GithubLink) },
                new HyperlinkButton { Content = BilibiliLink, NavigateUri = new Uri(BilibiliLink) }
            }
        };
        var dialog = CreateDialog(Tr("About"), panel, closeButtonText: Tr("Close"));
        await dialog.ShowAsync();
    }

    private async Task ShowCheatEnginePromptAsync()
    {
        var dontShowAgain = new CheckBox { Content = Tr("Don't show again") };
        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = Tr(".ct/.cetrainer files require Cheat Engine to run. Please install Cheat Engine and open these files with it."),
                    TextWrapping = TextWrapping.Wrap
                },
                new HyperlinkButton
                {
                    Content = "https://www.cheatengine.org",
                    NavigateUri = new Uri("https://www.cheatengine.org")
                },
                dontShowAgain
            }
        };
        var dialog = CreateDialog(Tr("Cheat Engine Required"), content, closeButtonText: Tr("OK"));
        await dialog.ShowAsync();
        if (dontShowAgain.IsChecked == true)
        {
            _settings.ShowCEPrompt = false;
            await _settingsService.SaveAsync(_settings);
        }
    }

    private static FrameworkElement LabeledControl(string label, Control control) =>
        new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = label },
                control
            }
        };

    private string GetCheatEngineStatus(string path) =>
        File.Exists(Path.Combine(path, "Cheat Engine.exe"))
            ? Tr("Cheat Engine is installed.")
            : Tr("Please select a valid Cheat Engine installation path.");

    private string GetCheatEvolutionStatus(string path)
    {
        if (File.Exists(Path.Combine(path, "CheatEvolution_patched.exe")))
        {
            return Tr("Cheat Evolution patch file is present.");
        }

        return File.Exists(Path.Combine(path, "CheatEvolution.exe"))
            ? Tr("Cheat Evolution is installed and ready to patch.")
            : Tr("Invalid Cheat Evolution installation path.");
    }

    private async Task ShowTrainerManagementDialogAsync()
    {
        var originalFlingServer = _settings.FlingDownloadServer;
        var originalEnableXiaoxing = _settings.EnableXiaoXing;

        var enableGcm = new CheckBox { Content = Tr("Enable search for GCM and Other trainers"), IsChecked = _settings.EnableGCM };
        var autoUpdateGcmData = new CheckBox { Content = Tr("Update GCM data automatically"), IsChecked = _settings.AutoUpdateGCMData };
        var autoUpdateGcmTrainers = new CheckBox { Content = Tr("Update GCM trainers automatically"), IsChecked = _settings.AutoUpdateGCMTrainers };

        var flingServer = new ComboBox { ItemsSource = new[] { "gcm", "official" }, SelectedItem = _settings.FlingDownloadServer };
        var removeFlingMusic = new CheckBox { Content = Tr("Remove trainer background music"), IsChecked = _settings.RemoveFlingBgMusic };
        var autoUpdateFlingData = new CheckBox { Content = Tr("Update FLiNG data automatically"), IsChecked = _settings.AutoUpdateFlingData };
        var autoUpdateFlingTrainers = new CheckBox { Content = Tr("Update FLiNG trainers automatically"), IsChecked = _settings.AutoUpdateFlingTrainers };

        var enableXiaoxing = new CheckBox { Content = Tr("Enable search for XiaoXing trainers"), IsChecked = _settings.EnableXiaoXing };
        var unlockXiaoxing = new CheckBox { Content = Tr("Unlock all XiaoXing functions"), IsChecked = _settings.UnlockXiaoXing };
        var autoUpdateXiaoxingData = new CheckBox { Content = Tr("Update XiaoXing data automatically"), IsChecked = _settings.AutoUpdateXiaoXingData };
        var autoUpdateXiaoxingTrainers = new CheckBox { Content = Tr("Update XiaoXing trainers automatically"), IsChecked = _settings.AutoUpdateXiaoXingTrainers };

        var cheatEnginePathBox = new TextBox { Text = _settings.CePath, IsReadOnly = true };
        var cheatEngineStatus = new TextBlock { Text = GetCheatEngineStatus(_settings.CePath) };
        var addZhCn = new CheckBox { Content = Tr("Add Simplified Chinese"), IsChecked = false };
        var enableCt = new CheckBox { Content = Tr("Enable search for Cheat Tables"), IsChecked = _settings.EnableCT };
        var autoUpdateCtData = new CheckBox { Content = Tr("Update Cheat Table data automatically"), IsChecked = _settings.AutoUpdateCTData };
        var autoUpdateCtTrainers = new CheckBox { Content = Tr("Update Cheat Table trainers automatically"), IsChecked = _settings.AutoUpdateCTTrainers };

        var weModPathBox = new TextBox { Text = _settings.WeModPath, IsReadOnly = true };
        var weModVersions = _customizationService.FindWeModVersions(_settings.WeModPath);
        var weModVersionCombo = new ComboBox { ItemsSource = weModVersions.Any() ? weModVersions : [Tr("Wand not installed")], SelectedIndex = 0 };
        var patchMethodCombo = new ComboBox { ItemsSource = new[] { "yearly_sub", "gifted_sub" }, SelectedIndex = 0 };
        var enableWeModPro = new CheckBox { Content = Tr("Activate Wand Pro"), IsChecked = false };
        var disableWeModUpdate = new CheckBox { Content = Tr("Disable Wand Auto Update"), IsChecked = false };
        var deleteOtherWeModVersions = new CheckBox { Content = Tr("Delete All Other Wand Versions"), IsChecked = false };

        var cevoPathBox = new TextBox { Text = _settings.CevoPath, IsReadOnly = true };
        var cevoStatus = new TextBlock { Text = GetCheatEvolutionStatus(_settings.CevoPath) };
        var enableCevoPro = new CheckBox { Content = Tr("Activate PRO"), IsChecked = false };

        var cheatEngineApplyButton = new Button { Content = Tr("Apply Cheat Engine Changes") };
        cheatEngineApplyButton.Click += async (_, _) =>
        {
            if (addZhCn.IsChecked == true)
            {
                var success = await _customizationService.AddCheatEngineTranslationAsync(cheatEnginePathBox.Text);
                ShowNotification("Cheat Engine", Tr(success ? "Successfully added translation files." : "Failed to add translation files."), success ? InfoBarSeverity.Success : InfoBarSeverity.Error);
            }
        };

        var wemodApplyButton = new Button { Content = Tr("Apply Wand Changes") };
        wemodApplyButton.Click += async (_, _) =>
        {
            var messages = await _customizationService.ApplyWeModCustomizationAsync(
                weModPathBox.Text,
                _customizationService.FindWeModVersions(weModPathBox.Text),
                weModVersionCombo.SelectedItem?.ToString() ?? string.Empty,
                patchMethodCombo.SelectedItem?.ToString() ?? "yearly_sub",
                enableWeModPro.IsChecked == true,
                disableWeModUpdate.IsChecked == true,
                deleteOtherWeModVersions.IsChecked == true);

            foreach (var message in messages)
            {
                AddStatusLine(LocalizeUserText(message), message.Contains("fail", StringComparison.OrdinalIgnoreCase) ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
            }
        };

        var cevoApplyButton = new Button { Content = Tr("Apply Cheat Evolution Changes") };
        cevoApplyButton.Click += async (_, _) =>
        {
            if (enableCevoPro.IsChecked == true)
            {
                var success = await _customizationService.ApplyCheatEvolutionPatchAsync(cevoPathBox.Text);
                ShowNotification("Cheat Evolution", Tr(success ? "Patch successful." : "Patch failed."), success ? InfoBarSeverity.Success : InfoBarSeverity.Error);
                cevoStatus.Text = GetCheatEvolutionStatus(cevoPathBox.Text);
            }
        };

        var cheatEngineBrowseButton = new Button { Content = Tr("Browse") };
        cheatEngineBrowseButton.Click += async (_, _) =>
        {
            var path = await PickFolderAsync();
            if (!string.IsNullOrWhiteSpace(path))
            {
                cheatEnginePathBox.Text = path;
                cheatEngineStatus.Text = GetCheatEngineStatus(path);
            }
        };

        var wemodBrowseButton = new Button { Content = Tr("Browse") };
        wemodBrowseButton.Click += async (_, _) =>
        {
            var path = await PickFolderAsync();
            if (!string.IsNullOrWhiteSpace(path))
            {
                weModPathBox.Text = path;
                weModVersions = _customizationService.FindWeModVersions(path);
                weModVersionCombo.ItemsSource = weModVersions.Any() ? weModVersions : [Tr("Wand not installed")];
                weModVersionCombo.SelectedIndex = 0;
            }
        };

        var cevoBrowseButton = new Button { Content = Tr("Browse") };
        cevoBrowseButton.Click += async (_, _) =>
        {
            var path = await PickFolderAsync();
            if (!string.IsNullOrWhiteSpace(path))
            {
                cevoPathBox.Text = path;
                cevoStatus.Text = GetCheatEvolutionStatus(path);
            }
        };

        var tabs = new TabView();
        tabs.TabItems.Add(new TabViewItem
        {
            Header = "GCM",
            Content = new StackPanel
            {
                Spacing = 12,
                Children = { enableGcm, autoUpdateGcmData, autoUpdateGcmTrainers }
            }
        });
        tabs.TabItems.Add(new TabViewItem
        {
            Header = "FLiNG",
            Content = new StackPanel
            {
                Spacing = 12,
                Children = { LabeledControl(Tr("Download server"), flingServer), removeFlingMusic, autoUpdateFlingData, autoUpdateFlingTrainers }
            }
        });
        tabs.TabItems.Add(new TabViewItem
        {
            Header = "XiaoXing",
            Content = new StackPanel
            {
                Spacing = 12,
                Children = { enableXiaoxing, unlockXiaoxing, autoUpdateXiaoxingData, autoUpdateXiaoxingTrainers }
            }
        });
        tabs.TabItems.Add(new TabViewItem
        {
            Header = "Cheat Engine",
            Content = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children = { cheatEngineStatus, LabeledControl(Tr("Cheat Engine path"), cheatEnginePathBox), cheatEngineBrowseButton, addZhCn, enableCt, autoUpdateCtData, autoUpdateCtTrainers, cheatEngineApplyButton }
                }
            }
        });
        tabs.TabItems.Add(new TabViewItem
        {
            Header = "Wand",
            Content = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children = { LabeledControl(Tr("Wand path"), weModPathBox), wemodBrowseButton, LabeledControl(Tr("Installed versions"), weModVersionCombo), LabeledControl(Tr("Patch method"), patchMethodCombo), enableWeModPro, disableWeModUpdate, deleteOtherWeModVersions, wemodApplyButton }
                }
            }
        });
        tabs.TabItems.Add(new TabViewItem
        {
            Header = "Cheat Evolution",
            Content = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children = { cevoStatus, LabeledControl(Tr("Cheat Evolution path"), cevoPathBox), cevoBrowseButton, enableCevoPro, cevoApplyButton }
                }
            }
        });

        var dialog = CreateDialog(Tr("Trainer Management"), tabs, Tr("Save"), Tr("Close"));
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _settings.EnableGCM = enableGcm.IsChecked == true;
        _settings.AutoUpdateGCMData = autoUpdateGcmData.IsChecked == true;
        _settings.AutoUpdateGCMTrainers = autoUpdateGcmTrainers.IsChecked == true;
        _settings.FlingDownloadServer = flingServer.SelectedItem?.ToString() ?? _settings.FlingDownloadServer;
        _settings.RemoveFlingBgMusic = removeFlingMusic.IsChecked == true;
        _settings.AutoUpdateFlingData = autoUpdateFlingData.IsChecked == true;
        _settings.AutoUpdateFlingTrainers = autoUpdateFlingTrainers.IsChecked == true;
        _settings.EnableXiaoXing = enableXiaoxing.IsChecked == true;
        _settings.UnlockXiaoXing = unlockXiaoxing.IsChecked == true;
        _settings.AutoUpdateXiaoXingData = autoUpdateXiaoxingData.IsChecked == true;
        _settings.AutoUpdateXiaoXingTrainers = autoUpdateXiaoxingTrainers.IsChecked == true;
        _settings.CePath = cheatEnginePathBox.Text;
        _settings.EnableCT = enableCt.IsChecked == true;
        _settings.AutoUpdateCTData = autoUpdateCtData.IsChecked == true;
        _settings.AutoUpdateCTTrainers = autoUpdateCtTrainers.IsChecked == true;
        _settings.WeModPath = weModPathBox.Text;
        _settings.CevoPath = cevoPathBox.Text;
        await _settingsService.SaveAsync(_settings);

        if (!string.Equals(originalFlingServer, _settings.FlingDownloadServer, StringComparison.Ordinal))
        {
            _ = _trainerCatalogService.FetchFlingDataAsync(_settings);
        }

        if (!originalEnableXiaoxing && _settings.EnableXiaoXing)
        {
            _ = _trainerCatalogService.FetchXiaoXingDataAsync();
        }

        ShowNotification(Tr("Trainer Management"), Tr("Trainer management settings saved."), InfoBarSeverity.Success);
    }

    private async Task ShowUploadTrainerDialogAsync()
    {
        var contactBox = new TextBox { PlaceholderText = Tr("Email (optional)") };
        var trainerNameBox = new TextBox { PlaceholderText = Tr("Trainer name") };
        var trainerSourceBox = new TextBox { PlaceholderText = Tr("Original URL or author (optional)") };
        var notesBox = new TextBox { AcceptsReturn = true, Height = 100, PlaceholderText = Tr("Additional notes") };
        var selectedFileBox = new TextBox { IsReadOnly = true, PlaceholderText = Tr("Select a trainer file") };
        var progressBar = new ProgressBar { Minimum = 0, Maximum = 100, Visibility = Visibility.Collapsed };
        var browseButton = new Button { Content = Tr("Browse") };

        browseButton.Click += async (_, _) =>
        {
            var file = await PickSingleFileAsync("*");
            if (file is not null)
            {
                selectedFileBox.Text = file.Path;
                if (string.IsNullOrWhiteSpace(trainerNameBox.Text))
                {
                    trainerNameBox.Text = file.Name;
                }
            }
        };

        var panel = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                LabeledControl(Tr("Contact info"), contactBox),
                LabeledControl(Tr("Trainer name"), trainerNameBox),
                LabeledControl(Tr("Trainer source"), trainerSourceBox),
                LabeledControl(Tr("Trainer file"), selectedFileBox),
                browseButton,
                LabeledControl(Tr("Additional notes"), notesBox),
                progressBar
            }
        };

        var dialog = CreateDialog(Tr("Upload Trainer"), panel, Tr("Upload"), Tr("Cancel"));
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            args.Cancel = true;
            if (string.IsNullOrWhiteSpace(selectedFileBox.Text) || !File.Exists(selectedFileBox.Text))
            {
                ShowNotification(Tr("Upload"), Tr("Please select a valid trainer file."), InfoBarSeverity.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(trainerNameBox.Text))
            {
                ShowNotification(Tr("Upload"), Tr("Please provide a trainer name."), InfoBarSeverity.Warning);
                return;
            }

            var metadata = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["uploader-contact"] = contactBox.Text,
                ["trainer-name"] = trainerNameBox.Text,
                ["trainer-source"] = trainerSourceBox.Text,
                ["notes"] = notesBox.Text
            });

            var authDocument = await _fileDownloadService.GetSignedUploadUrlAsync(selectedFileBox.Text, metadata);
            if (authDocument is null)
            {
                ShowNotification(Tr("Upload"), Tr("Upload backend is not configured."), InfoBarSeverity.Error);
                return;
            }

            var root = authDocument.RootElement;
            var uploadUrl = root.TryGetProperty("uploadUrl", out var uploadUrlElement) ? uploadUrlElement.GetString() : null;
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("requiredHeaders", out var headerElement))
            {
                foreach (var property in headerElement.EnumerateObject())
                {
                    headers[property.Name] = property.Value.ToString();
                }
            }

            if (string.IsNullOrWhiteSpace(uploadUrl))
            {
                ShowNotification(Tr("Upload"), Tr("Failed to retrieve upload URL."), InfoBarSeverity.Error);
                return;
            }

            progressBar.Visibility = Visibility.Visible;
            var success = await _fileDownloadService.UploadFileAsync(
                uploadUrl,
                headers,
                selectedFileBox.Text,
                new Progress<int>(value => progressBar.Value = value));

            if (success)
            {
                ShowNotification(Tr("Upload"), Tr("Upload successful. Thank you for your contribution."), InfoBarSeverity.Success);
                dialog.Hide();
            }
            else
            {
                ShowNotification(Tr("Upload"), Tr("Upload failed."), InfoBarSeverity.Error);
            }
        };

        await dialog.ShowAsync();
    }

    private async Task LaunchSelectedTrainerAsync()
    {
        if (InstalledTrainersListView.SelectedItem is not InstalledTrainer trainer)
        {
            return;
        }

        var windowHandle = WindowNative.GetWindowHandle(this);
        UpdateOperationStatus(Tr("Launching trainer..."));
        var result = await _trainerLibraryService.LaunchTrainerAsync(trainer, _settings.SafePath, _settings.ShowCEPrompt, windowHandle);
        if (result.RequiresCheatEnginePrompt)
        {
            await ShowCheatEnginePromptAsync();
            result = await _trainerLibraryService.LaunchTrainerAsync(trainer, _settings.SafePath, showCePrompt: false, windowHandle);
        }

        if (!result.Started)
        {
            var message = string.IsNullOrWhiteSpace(result.Message)
                ? Tr("Failed to start the trainer.")
                : LocalizeUserText(result.Message);
            ShowNotification(Tr("Launch Failed"), message, InfoBarSeverity.Error);
            AddStatusLine(message, InfoBarSeverity.Error);
            UpdateOperationStatus(Tr("Idle"));
            return;
        }

        AddStatusLine(Trf("Launch {0}", trainer.DisplayName), InfoBarSeverity.Success);
        UpdateOperationStatus(Tr("Idle"));
    }

    private async void OnInstalledSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        await RefreshInstalledTrainersAsync();
    }

    private async void OnSearchClick(object sender, RoutedEventArgs e)
    {
        await SearchAsync();
    }

    private async void OnDownloadSearchBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await SearchAsync();
        }
    }

    private async void OnInstalledTrainerDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        await LaunchSelectedTrainerAsync();
    }

    private async void OnDownloadResultDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (DownloadResultsListView.SelectedItem is TrainerCatalogEntry trainer)
        {
            await QueueDownloadAsync(trainer);
        }
    }

    private async void OnLaunchClick(object sender, RoutedEventArgs e)
    {
        await LaunchSelectedTrainerAsync();
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (InstalledTrainersListView.SelectedItem is not InstalledTrainer trainer)
        {
            return;
        }

        if (!await ConfirmAsync(Tr("Delete Trainer"), Trf("Delete {0}?", trainer.DisplayName), Tr("Delete"), Tr("Cancel")))
        {
            return;
        }

        try
        {
            await _trainerLibraryService.DeleteTrainerAsync(trainer);
            await RefreshInstalledTrainersAsync();
            ShowNotification(Tr("Delete"), Tr("Trainer deleted."), InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowNotification(Tr("Delete Failed"), ex.Message, InfoBarSeverity.Error);
        }
    }

    private async void OnChangePathClick(object sender, RoutedEventArgs e)
    {
        var folder = await PickFolderAsync();
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        var changedPath = Path.Combine(folder, "GCM Trainers");
        if (string.Equals(changedPath, _settings.DownloadPath, StringComparison.OrdinalIgnoreCase))
        {
            ShowNotification(Tr("Path"), Tr("Please choose a new path."), InfoBarSeverity.Warning);
            return;
        }

        AddStatusLine(Tr("Migrating existing trainers..."));
        await _trainerLibraryService.MoveDirectoryAsync(_settings.DownloadPath, changedPath);
        _settings.DownloadPath = changedPath;
        await _settingsService.SaveAsync(_settings);
        DownloadPathTextBox.Text = changedPath;
        await RefreshInstalledTrainersAsync();
        ShowNotification(Tr("Path"), Tr("Migration complete."), InfoBarSeverity.Success);
    }

    private async void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        await ShowSettingsDialogAsync();
    }

    private async void OnImportClick(object sender, RoutedEventArgs e)
    {
        var files = await PickFilesAsync(".exe", ".ct", ".cetrainer");
        if (files is null || files.Length == 0)
        {
            return;
        }

        await _trainerLibraryService.ImportFilesAsync(files.Select(static file => file.Path), _settings.DownloadPath);
        await RefreshInstalledTrainersAsync();

        if (await ConfirmAsync(Tr("Delete Original Trainers"), Tr("Do you want to delete the original trainer files?"), Tr("Delete"), Tr("Keep")))
        {
            foreach (var file in files)
            {
                try
                {
                    File.Delete(file.Path);
                }
                catch
                {
                }
            }
        }

        ShowNotification(Tr("Import"), Tr("Trainer import complete."), InfoBarSeverity.Success);
    }

    private void OnOpenTrainerDirectoryClick(object sender, RoutedEventArgs e)
    {
        _processService.OpenWithShell(_settings.DownloadPath);
    }

    private async void OnWhitelistClick(object sender, RoutedEventArgs e)
    {
        var confirmed = await ConfirmAsync(
            Tr("Administrator Access Required"),
            Tr("Adding paths to the Windows Defender whitelist requires administrator rights. Continue?"),
            Tr("Continue"),
            Tr("Cancel"));
        if (!confirmed)
        {
            return;
        }

        var success = await _customizationService.AddWindowsDefenderWhitelistAsync([AppPaths.DownloadTempDirectory, _settings.DownloadPath]);
        ShowNotification(Tr("Whitelist"), Tr(success ? "Paths added to Windows Defender whitelist." : "Failed to add paths to whitelist."), success ? InfoBarSeverity.Success : InfoBarSeverity.Error);
    }

    private async void OnAboutClick(object sender, RoutedEventArgs e)
    {
        await ShowAboutDialogAsync();
    }

    private async void OnUpdateTranslationsClick(object sender, RoutedEventArgs e)
    {
        if (!_backendConfig.HasSignedDownloadConfig)
        {
            ShowNotification(Tr("Translation Data"), Tr("This source requires the private Game-Zone backend configuration, which is not present in this workspace."), InfoBarSeverity.Warning);
            return;
        }

        var success = await _trainerCatalogService.FetchTrainerTranslationsAsync();
        ShowNotification(Tr("Translation Data"), Tr(success ? "Translation data updated." : "Translation data update failed."), success ? InfoBarSeverity.Success : InfoBarSeverity.Error);
    }

    private async void OnUpdateSearchDataClick(object sender, RoutedEventArgs e)
    {
        var fling = await _trainerCatalogService.FetchFlingDataAsync(_settings);
        var gcm = !_backendConfig.HasSignedDownloadConfig || !_settings.EnableGCM || await _trainerCatalogService.FetchGcmDataAsync();
        var xiaoxing = !_backendConfig.HasSignedDownloadConfig || !_settings.EnableXiaoXing || await _trainerCatalogService.FetchXiaoXingDataAsync();
        var ct = !_backendConfig.HasSignedDownloadConfig || !_settings.EnableCT || await _trainerCatalogService.FetchCheatTableDataAsync();
        var success = fling && gcm && xiaoxing && ct;
        ShowNotification(Tr("Search Data"), Tr(success ? "Trainer search data updated." : "One or more data sources failed to update."), success ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
    }

    private async void OnUpdateTrainersClick(object sender, RoutedEventArgs e)
    {
        await UpdateInstalledTrainersAsync(autoCheck: false);
    }

    private async void OnTrainerManagementClick(object sender, RoutedEventArgs e)
    {
        await ShowTrainerManagementDialogAsync();
    }

    private async void OnUploadTrainerClick(object sender, RoutedEventArgs e)
    {
        await ShowUploadTrainerDialogAsync();
    }

    private void OnBrowseAllTrainersClick(object sender, RoutedEventArgs e)
    {
        _processService.OpenUrl(AllTrainersLink);
    }
}
