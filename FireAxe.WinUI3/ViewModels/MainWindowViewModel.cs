// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\MainWindowViewModel.cs
// ReactiveUI -> CommunityToolkit.Mvvm:
//   * ObservableAsPropertyHelper / WhenAnyValue pipelines  -> PropertyChanged-driven recompute
//   * ReactiveCommand (25)                                 -> RelayCommand / AsyncRelayCommand fields
//   * Interaction<T,U> (25)                                -> IDialogService / IPickerService / IWindowService
//   * Avalonia DispatcherTimer.Run(...)                    -> IUiDispatcher.StartTimer
//   * MainWindow.Clipboard                                 -> IClipboardService
//   * MessageBus.Current.Listen<AddonNodeJumpMessage>      -> WeakReferenceMessenger
//   * IActivatableViewModel (empty WhenActivated)          -> dropped
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FireAxe.Resources;
using FireAxe.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FireAxe.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase, ISaveable, IDisposable
{
    private sealed class AddonRootParentSettings : IAddonRootParentSettings
    {
        private readonly MainWindowViewModel _mainWindowViewModel;

        internal AddonRootParentSettings(MainWindowViewModel mainWindowViewModel)
        {
            _mainWindowViewModel = mainWindowViewModel;
        }

        public bool IsAutoUpdateWorkshopItem => _mainWindowViewModel._appSettings.IsAutoUpdateWorkshopItem;

        public VpkAddonConflictCheckSettings VpkAddonConflictCheckSettings => _mainWindowViewModel._vpkAddonConflictCheckSettings;
    }

    private static readonly TimeSpan CheckClipboardInterval = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan AutoRedownloadInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan BackupInterval = TimeSpan.FromMinutes(1);

    private bool _disposed = false;

    private bool _inited = false;

    private readonly AppSettings _appSettings;
    private readonly IWindowService _windowService;
    private readonly IDialogService _dialogService;
    private readonly IPickerService? _pickerService;
    private readonly IClipboardService? _clipboardService;
    private readonly IUiDispatcher? _uiDispatcher;
    private readonly IDownloadService _downloadService;
    private readonly HttpClient _httpClient;
    private readonly DownloadItemListViewModel _downloadItemListViewModel;

    private AddonRoot? _addonRoot = null;

    private bool _isOpeningDirectory = false;

    private readonly AddonRootParentSettings _addonRootParentSettings;
    private VpkAddonConflictCheckSettings _vpkAddonConflictCheckSettings = VpkAddonConflictCheckSettings.Default;

    private DateTime? _lastPushDateTime = null;

    private AddonNodeExplorerViewModel? _addonNodeExplorerViewModel = null;
    private AddonNodeExplorerViewModel? _observedExplorerViewModel = null;

    private bool _isSyncingExplorerSettings = false;

    private bool _hasSelection = false;
    private bool _isEmptyDirectory = true;
    private string _titleExtraInfo = "";

    private bool _isCheckingUpdate = false;
    private object? _checkUpdateActivity = null;
    private CancellationTokenSource? _checkUpdateCts = null;

    private readonly IDisposable? _checkClipboardTimer;
    private bool _isCheckingClipboard = false;
    private string? _lastClipboardText = null;

    private readonly IDisposable? _autoRedownloadTimer;

    private readonly IDisposable? _backupTimer;

    public MainWindowViewModel(
        AppSettings appSettings,
        IWindowService windowService,
        IDialogService dialogService,
        IDownloadService downloadService,
        HttpClient httpClient,
        DownloadItemListViewModel downloadItemListViewModel)
    {
        ArgumentNullException.ThrowIfNull(appSettings);
        ArgumentNullException.ThrowIfNull(windowService);
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(downloadService);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(downloadItemListViewModel);

        _appSettings = appSettings;
        _windowService = windowService;
        _dialogService = dialogService;
        _downloadService = downloadService;
        _httpClient = httpClient;
        _downloadItemListViewModel = downloadItemListViewModel;

        _pickerService = AppServices.TryGetService<IPickerService>();
        _clipboardService = AppServices.TryGetService<IClipboardService>();
        _uiDispatcher = AppServices.TryGetService<IUiDispatcher>();

        _addonRootParentSettings = new(this);

        OpenDirectoryCommand = new AsyncRelayCommand(OpenDirectoryAsync);
        CloseDirectoryCommand = new RelayCommand(CloseDirectory, () => _addonRoot != null);
        ImportCommand = new AsyncRelayCommand(ImportAsync, () => _addonRoot != null);
        ImportAddonRootFileCommand = new AsyncRelayCommand(ImportAddonRootFileAsync, () => _addonRoot != null);
        OpenSettingsWindowCommand = new RelayCommand(() => _windowService.OpenSettingsWindow());
        OpenProblemListWindowCommand = new RelayCommand(() => _windowService.OpenProblemListWindow(), () => _addonRoot != null);
        OpenDownloadListWindowCommand = new RelayCommand(() => _windowService.OpenDownloadListWindow());
        OpenTagManagerWindowCommand = new RelayCommand(() => _windowService.OpenTagManagerWindow(), () => _addonRoot != null);
        OpenVpkConflictListWindowCommand = new RelayCommand(() => _windowService.OpenVpkConflictListWindow(), () => _addonRoot != null);
        OpenReplacementOverviewWindowCommand = new RelayCommand(() => _windowService.OpenReplacementOverviewWindow(), () => _addonRoot != null);
        OpenWorkshopVpkFinderWindowCommand = new RelayCommand(() => _windowService.OpenWorkshopVpkFinderWindow());
        OpenFileCleanerWindowCommand = new RelayCommand(() => _windowService.OpenFileCleanerWindow(), () => _addonRoot != null);
        OpenAddonNameAutoSetterWindowCommand = new RelayCommand(() => _windowService.OpenAddonNameAutoSetterWindow(), () => _addonRoot != null);
        OpenFileNameFixerWindowCommand = new RelayCommand(() => _windowService.OpenFileNameFixerWindow(), () => _addonRoot != null);
        PushCommand = new AsyncRelayCommand(PushAsync, () => _addonRoot != null);
        CheckCommand = new RelayCommand(Check, () => _addonRoot != null);
        ClearCachesCommand = new RelayCommand(ClearCaches, () => _addonRoot != null);
        RandomlySelectCommand = new AsyncRelayCommand(() => ShowItemsRandomSelectedAsync(RandomlySelect()), () => _addonRoot != null);
        RandomlySelectForSelectedItemsCommand = new AsyncRelayCommand(() => ShowItemsRandomSelectedAsync(RandomlySelect(true)), () => HasSelection);
        DeleteRedundantVpkFilesCommand = new AsyncRelayCommand(() => DeleteRedundantVpkFilesAsync(), () => _addonRoot != null);
        DeleteRedundantVpkFilesForSelectedItemsCommand = new AsyncRelayCommand(() => DeleteRedundantVpkFilesAsync(true), () => HasSelection);
        ExportSelectedItemsAsAddonRootFile = new AsyncRelayCommand(ExportSelectedItemsAsAddonRootFileAsync, () => HasSelection);
        DeleteAddonsWithMissingFilesCommand = new AsyncRelayCommand(() => DeleteAddonsWithMissingFilesAsync(), () => _addonRoot != null);
        DeleteAddonsWithMissingFilesForSelectedItemsCommand = new AsyncRelayCommand(() => DeleteAddonsWithMissingFilesAsync(true), () => HasSelection);
        OpenAboutWindowCommand = new RelayCommand(() => _windowService.OpenAboutWindow());
        CheckUpdateCommand = new RelayCommand(() => CheckUpdate(false));
        RunGameCommand = new AsyncRelayCommand(RunGameAsync);

        _appSettings.PropertyChanged += OnAppSettingsPropertyChanged;
        UpdateVpkAddonConflictCheckSettings();

        // Clipboard / auto-redownload / backup timers (were DispatcherTimer.Run(...)).
        _checkClipboardTimer = _uiDispatcher?.StartTimer(CheckClipboardInterval, () =>
        {
            CheckClipboard();
            return true;
        });
        _autoRedownloadTimer = _uiDispatcher?.StartTimer(AutoRedownloadInterval, () =>
        {
            AutoRedownload();
            return true;
        });
        _backupTimer = _uiDispatcher?.StartTimer(BackupInterval, () =>
        {
            BackUpIfNeed();
            return true;
        });

        WeakReferenceMessenger.Default.Register<AddonNodeJumpMessage>(this, OnAddonNodeJumpMessage);
    }

    public event Action? ShowCheckingUpdateWindow = null;

    public event Action? CloseCheckingUpdateWindow = null;

    public AppSettings AppSettings => _appSettings;

    public bool RequestSave
    {
        get
        {
            if (_addonRoot == null)
            {
                return false;
            }
            return _addonRoot.RequestSave;
        }
        set
        {
            if (_addonRoot != null)
            {
                _addonRoot.RequestSave = value;
            }
        }
    }

    public AddonRoot? AddonRoot
    {
        get => _addonRoot;
        private set
        {
            if (value == _addonRoot)
            {
                return;
            }

            if (_addonRoot != null)
            {
                _addonRoot.Save();

                DisposeAddonRoot();
            }

            _addonRoot = value;
            UpdateDerivedState();

            AddonNodeExplorerViewModel = null;

            if (_addonRoot is not null)
            {
                using var blockAutoCheck = _addonRoot.BlockAutoCheck();

                _addonRoot.NewDownloadItem += OnAddonRootNewDownloadItem;
                _addonRoot.Pushed += OnAddonRootPushed;
                _addonRoot.ParentSettings = _addonRootParentSettings;
                _addonRoot.TaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
                _addonRoot.DownloadService = _downloadService;
                _addonRoot.HttpClient = _httpClient;

                try
                {
                    _addonRoot.LoadFile();
                }
                catch (AddonRootDeserializationException)
                {
                    DisposeAddonRoot();
                    OnPropertyChanged(nameof(AddonRoot));
                    UpdateDerivedState();
                    throw;
                }

                _addonRoot.GamePath = _appSettings.GamePath;
                _addonRoot.Check();

                AddonNodeExplorerViewModel = new(_addonRoot);
            }

            OnPropertyChanged(nameof(AddonRoot));
            UpdateDerivedState();

            void DisposeAddonRoot()
            {
                _addonRoot!.NewDownloadItem -= OnAddonRootNewDownloadItem;
                _addonRoot.Pushed -= OnAddonRootPushed;
                _addonRoot.DisposeAsync().AsTask(); // TODO: await (unchanged from the Avalonia version)
                _addonRoot = null;
            }
        }
    }

    public bool IsOpeningDirectory
    {
        get => _isOpeningDirectory;
        private set
        {
            if (SetProperty(ref _isOpeningDirectory, value))
            {
                UpdateDerivedState();
            }
        }
    }

    public bool IsEmptyDirectory => _isEmptyDirectory;

    public AddonNodeExplorerViewModel? AddonNodeExplorerViewModel
    {
        get => _addonNodeExplorerViewModel;
        private set
        {
            if (!SetProperty(ref _addonNodeExplorerViewModel, value))
            {
                return;
            }

            ObserveExplorerViewModel(value);
            SyncExplorerSettingsFromAppSettings(value);
            UpdateHasSelection();
        }
    }

    public string TitleExtraInfo => _titleExtraInfo;

    public bool HasSelection => _hasSelection;

    public DateTime? LastPushDateTime
    {
        get => _lastPushDateTime;
        private set => SetProperty(ref _lastPushDateTime, value);
    }

    public IAsyncRelayCommand OpenDirectoryCommand { get; }

    public IRelayCommand CloseDirectoryCommand { get; }

    public IAsyncRelayCommand ImportCommand { get; }

    public IAsyncRelayCommand ImportAddonRootFileCommand { get; }

    public IRelayCommand OpenSettingsWindowCommand { get; }

    public IRelayCommand OpenDownloadListWindowCommand { get; }

    public IRelayCommand OpenProblemListWindowCommand { get; }

    public IRelayCommand OpenTagManagerWindowCommand { get; }

    public IRelayCommand OpenVpkConflictListWindowCommand { get; }

    public IRelayCommand OpenReplacementOverviewWindowCommand { get; }

    public IRelayCommand OpenWorkshopVpkFinderWindowCommand { get; }

    public IRelayCommand OpenFileCleanerWindowCommand { get; }

    public IRelayCommand OpenAddonNameAutoSetterWindowCommand { get; }

    public IRelayCommand OpenFileNameFixerWindowCommand { get; }

    public IAsyncRelayCommand PushCommand { get; }

    public IRelayCommand CheckCommand { get; }

    public IRelayCommand ClearCachesCommand { get; }

    public IAsyncRelayCommand RandomlySelectCommand { get; }

    public IAsyncRelayCommand RandomlySelectForSelectedItemsCommand { get; }

    public IAsyncRelayCommand DeleteRedundantVpkFilesCommand { get; }

    public IAsyncRelayCommand DeleteRedundantVpkFilesForSelectedItemsCommand { get; }

    public IAsyncRelayCommand ExportSelectedItemsAsAddonRootFile { get; }

    public IAsyncRelayCommand DeleteAddonsWithMissingFilesCommand { get; }

    public IAsyncRelayCommand DeleteAddonsWithMissingFilesForSelectedItemsCommand { get; }

    public IRelayCommand OpenAboutWindowCommand { get; }

    public IRelayCommand CheckUpdateCommand { get; }

    public IAsyncRelayCommand RunGameCommand { get; }

    public async void InitIfNot()
    {
        if (_inited)
        {
            return;
        }
        _inited = true;

        if (_appSettings.IsAutoCheckUpdateEnabled)
        {
            CheckUpdate(true);
        }

        // Try to open the LastOpenDirectory.
        var lastOpenDir = _appSettings.LastOpenDirectory;
        if (lastOpenDir != null)
        {
            _appSettings.LastOpenDirectory = null;
            _appSettings.Save();
            if (Directory.Exists(lastOpenDir))
            {
                await OpenDirectoryAsync(lastOpenDir);
            }
        }
    }

    public async Task OpenDirectoryAsync()
    {
        if (_pickerService is null)
        {
            return;
        }

        var path = await _pickerService.PickFolderAsync(new FolderPickerOptions { Title = Texts.OpenDirectory });
        if (path == null)
        {
            return;
        }
        if (GamePathUtils.IsAddonsPath(path))
        {
            await _dialogService.ShowInfoAsync(Texts.DontOpenGameAddonsDirectory, Texts.Error);
            return;
        }
        await OpenDirectoryAsync(path);
    }

    public async Task OpenDirectoryAsync(string dirPath)
    {
        ArgumentNullException.ThrowIfNull(dirPath);

        if (IsOpeningDirectory)
        {
            return;
        }

        IsOpeningDirectory = true;
        try
        {
            async Task OpenAsync()
            {
                if (GamePathUtils.IsAddonsPath(dirPath))
                {
                    return;
                }

                string versionFilePath = Path.Join(dirPath, AddonRoot.VersionFileName);
                if (File.Exists(versionFilePath))
                {
                    if (Version.TryParse(File.ReadAllText(versionFilePath), out var version))
                    {
                        if (version > AppGlobal.Version)
                        {
                            var message = $"{Texts.ConfirmOpenHigherVersionFileMessage}\n({dirPath} v{version.ToString(3)})";
                            if (!await _dialogService.ConfirmAsync(message, Texts.Warning))
                            {
                                return;
                            }
                        }
                    }
                }

                var addonRoot = new AddonRoot();
                addonRoot.DirectoryPath = dirPath;
                AddonRoot = addonRoot;

                _appSettings.LastOpenDirectory = dirPath;
            }

            if (_uiDispatcher is { } dispatcher && !dispatcher.HasThreadAccess)
            {
                await dispatcher.InvokeAsync(OpenAsync);
            }
            else
            {
                await OpenAsync();
            }
        }
        catch (AddonRootDeserializationException ex)
        {
            Log.Error(ex, "Exception occurred during the deserialization of the AddonRoot. Directory Path: {DirectoryPath}", dirPath);
            await _dialogService.ShowExceptionAsync(ex, Texts.AddonRootDeserializationExceptionMessage);
        }
        finally
        {
            IsOpeningDirectory = false;
        }
    }

    public void CloseDirectory()
    {
        AddonRoot = null;

        _appSettings.LastOpenDirectory = null;
    }

    public void Save()
    {
        if (_addonRoot != null)
        {
            _addonRoot.Save();
        }
    }

    public void Check()
    {
        AddonRoot?.Check();
    }

    public void ClearCaches()
    {
        if (_addonRoot != null)
        {
            foreach (var addonNode in _addonRoot.GetDescendants())
            {
                addonNode.ClearCaches();
            }
        }
    }

    public int RandomlySelect(bool forSelectedItems = false)
    {
        IEnumerable<AddonGroup>? targets = forSelectedItems ?
            AddonNodeExplorerViewModel?.SelectedNodes.SelectMany(node => node.GetSelfAndDescendants()).OfType<AddonGroup>() :
            AddonRoot?.GetDescendants().OfType<AddonGroup>();
        if (targets is null)
        {
            return 0;
        }
        int count = 0;
        foreach (var group in targets)
        {
            if (group.EnableOneChildRandomlyIfSingleRandom())
            {
                count++;
            }
        }
        return count;
    }

    public async void CheckUpdate(bool silenced)
    {
        if (!silenced)
        {
            ShowCheckingUpdateWindow?.Invoke();
        }
        if (_isCheckingUpdate)
        {
            return;
        }

        _isCheckingUpdate = true;
        var activity = new object();
        _checkUpdateActivity = activity;
        _checkUpdateCts = new();
        string? latestVersion = null;
        try
        {
            latestVersion = await AppGlobal.GetLatestVersionAsync(_appSettings.UpdateCheckRepository, _httpClient, _checkUpdateCts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        if (activity == _checkUpdateActivity)
        {
            _isCheckingUpdate = false;
            _checkUpdateActivity = null;
            _checkUpdateCts.Dispose();
            _checkUpdateCts = null;
        }

        CloseCheckingUpdateWindow?.Invoke();
        if (silenced)
        {
            if (latestVersion == null)
            {
                return;
            }
            if (latestVersion == _appSettings.SuppressedUpdateRequestVersion)
            {
                return;
            }
            if (latestVersion == AppGlobal.VersionString)
            {
                return;
            }
        }

        if (latestVersion == null)
        {
            await _dialogService.ShowInfoAsync(Texts.FailedToGetLatestVersion, Texts.Error);
        }
        else if (latestVersion == AppGlobal.VersionString)
        {
            await _dialogService.ShowInfoAsync(Texts.CurrentVersionLatestMessage, Texts.Update);
        }
        else
        {
            var textGoToDownload = Texts.GoToDownload;
            var textIgnore = Texts.Ignore;
            int choice = await _dialogService.ShowChoiceAsync(
                Texts.NewVersionAvailable.FormatNoThrow(latestVersion),
                Texts.Update,
                textGoToDownload,
                textIgnore);

            if (choice == 0)
            {
                Utils.OpenWebsite(AppGlobal.GetGithubReleasesUrl(_appSettings.UpdateCheckRepository));
            }
            else if (choice == 1)
            {
                _appSettings.SuppressedUpdateRequestVersion = latestVersion;
            }
        }
    }

    public void CancelCheckUpdate()
    {
        if (!_isCheckingUpdate)
        {
            return;
        }

        _isCheckingUpdate = false;
        _checkUpdateActivity = null;
        _checkUpdateCts!.Cancel();
        _checkUpdateCts!.Dispose();
        _checkUpdateCts = null;
    }

    public async void CheckClipboard()
    {
        if (_isCheckingClipboard)
        {
            return;
        }
        _isCheckingClipboard = true;

        async Task Check()
        {
            string? clipboardText = await GetClipboardText();
            if (_disposed)
            {
                return;
            }
            if (clipboardText == _lastClipboardText)
            {
                return;
            }
            _lastClipboardText = clipboardText;

            var explorerViewModel = AddonNodeExplorerViewModel;

            if (explorerViewModel != null && _appSettings.IsAutoDetectWorkshopItemLinkInClipboard)
            {
                if (clipboardText != null)
                {
                    if (PublishedFileUtils.TryParsePublishedFileIdLink(clipboardText, out var id))
                    {
                        // A collection link can't be added as a single addon, so tell the user
                        // how to import it instead of creating an addon that would fail to download.
                        bool? isCollection = null;
                        try
                        {
                            isCollection = await WorkshopCollectionUtils.IsWorkshopCollectionAsync(id, _httpClient, CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Exception occurred during checking whether the published file is a collection.");
                        }
                        if (isCollection == true)
                        {
                            await _dialogService.ShowInfoAsync(
                                Texts.WorkshopCollectionLinkDetectedMessage.FormatNoThrow(clipboardText),
                                Texts.Warning);
                            return;
                        }

                        bool confirm = await _dialogService.ConfirmAsync(
                            Texts.AutoDetectWorkshopItemLinkDialogMessage.FormatNoThrow(clipboardText), "");
                        if (confirm)
                        {
                            var addon = explorerViewModel.NewWorkshopAddon();
                            addon.PublishedFileId = id;
                        }
                    }
                }
            }
        }

        async Task<string?> GetClipboardText()
        {
            var clipboard = _clipboardService;
            if (clipboard == null)
            {
                return null;
            }
            try
            {
                return await clipboard.TryGetTextAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during opening clipboard");
            }
            return null;
        }

        await Check();

        _isCheckingClipboard = false;
    }

    public void AutoRedownload()
    {
        if (_addonRoot == null)
        {
            return;
        }
        if (!_appSettings.IsAutoRedownload)
        {
            return;
        }

        foreach (var addonNode in _addonRoot.GetDescendants())
        {
            if (addonNode is WorkshopVpkAddon workshopVpkAddon)
            {
                if (workshopVpkAddon.VpkFilePath == null)
                {
                    workshopVpkAddon.Check();
                }
            }
        }
    }

    public void BackUpIfNeed()
    {
        if (!_appSettings.IsFileBackupEnabled)
        {
            return;
        }
        AddonRoot?.BackUpIfNeed(_appSettings.MaxRetainedBackupFileCount, _appSettings.FileBackupIntervalMinutes);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        WeakReferenceMessenger.Default.UnregisterAll(this);

        _appSettings.PropertyChanged -= OnAppSettingsPropertyChanged;
        ObserveExplorerViewModel(null);

        AddonRoot = null;

        _checkClipboardTimer?.Dispose();
        _autoRedownloadTimer?.Dispose();
        _backupTimer?.Dispose();
    }

    private async Task ImportAsync()
    {
        if (_addonRoot is null)
        {
            return;
        }

        using var blockAutoCheck = _addonRoot.BlockAutoCheck();
        AddonRoot.ImportResult result;
        try
        {
            result = _addonRoot.Import();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during the import of the AddonRoot.");
            await _dialogService.ShowExceptionAsync(ex, Texts.ImportErrorMessage);
            return;
        }
        _addonRoot.Check();
        await _windowService.ShowImportResultAsync(result);
    }

    private async Task ImportAddonRootFileAsync()
    {
        var explorerViewModel = AddonNodeExplorerViewModel;
        if (explorerViewModel is null || _pickerService is null)
        {
            return;
        }

        var filePath = await _pickerService.PickFileAsync(new FilePickerOptions
        {
            Title = Texts.ImportAddonRootFile,
            FilePatterns = ["*.addonroot"]
        });
        if (filePath is null)
        {
            return;
        }

        var addonRoot = explorerViewModel.AddonRoot;
        using var blockAutoCheck = addonRoot.BlockAutoCheck();

        IEnumerable<AddonNodeSave> nodeSaves;
        try
        {
            nodeSaves = AddonRoot.Deserialize(File.ReadAllText(filePath)).Nodes;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception ocurred during importing .addonroot file: {FilePath}", filePath);
            await _dialogService.ShowExceptionAsync(ex);
            return;
        }

        var importedGroup = AddonNode.Create<AddonGroup>(addonRoot, explorerViewModel.CurrentGroup);
        importedGroup.Name = importedGroup.Parent.GetUniqueChildName(Texts.ImportedGroup);
        explorerViewModel.SelectNode(importedGroup);
        foreach (var nodeSave in nodeSaves)
        {
            AddonNode.LoadSave(nodeSave, addonRoot, importedGroup);
        }
        addonRoot.Check();
    }

    private async Task PushAsync()
    {
        if (_addonRoot == null)
        {
            return;
        }

        _addonRoot.Check();
        try
        {
            _addonRoot.Push();
        }
        catch (InvalidGamePathException)
        {
            await _dialogService.ShowInfoAsync(
                Texts.InvalidGamePathMessage.FormatNoThrow(_addonRoot.GamePath), Texts.Error);
            return;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException || (ex is IOException && (ex.HResult & 0xffff) == 1314))
        {
            Log.Warning(ex, "Push destination access or symbolic-link privilege denied.");
            await _dialogService.ShowErrorAsync(NativeUiTexts.PushPermissionHelp, Texts.Error);
            return;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during the push of the AddonRoot.");
            await _dialogService.ShowExceptionAsync(ex, Texts.PushErrorMessage);
            return;
        }
        await _dialogService.ShowInfoAsync(Texts.PushSuccessMessage, Texts.Success);
    }

    private async Task RunGameAsync()
    {
        var gamePath = _addonRoot?.GamePath ?? _appSettings.GamePath;
        if (gamePath.Length == 0 || !GamePathUtils.CheckValidity(gamePath))
        {
            await _dialogService.ShowInfoAsync(
                Texts.InvalidGamePathMessage.FormatNoThrow(gamePath), Texts.Error);
            return;
        }

        string? launchOptions;
        var customLaunchOptions = _appSettings.CustomLaunchOptions;
        if (customLaunchOptions.Length > 0)
        {
            launchOptions = customLaunchOptions;
        }
        else
        {
            launchOptions = SteamUtils.TryGetLaunchOptions(SteamUtils.Left4Dead2AppId);
            if (launchOptions is null)
            {
                // The steam launch options couldn't be read; let the user decide instead of refusing.
                bool confirm = await _dialogService.ConfirmAsync(Texts.CannotReadSteamLaunchOptionsMessage, Texts.Warning);
                if (!confirm)
                {
                    return;
                }
                launchOptions = "";
            }
        }

        var errorMessage = Utils.RunGame(gamePath, launchOptions);
        if (errorMessage is not null)
        {
            await _dialogService.ShowErrorAsync(errorMessage, Texts.Error);
        }
    }

    private async Task ExportSelectedItemsAsAddonRootFileAsync()
    {
        if (_pickerService is null)
        {
            return;
        }

        var filePath = await _pickerService.SaveFileAsync(new SaveFilePickerOptions
        {
            Title = Texts.SaveAddonRootFile,
            FilePatterns = ["*.addonroot"],
            DefaultFileExtension = ".addonroot"
        });
        if (filePath is null)
        {
            return;
        }

        try
        {
            var explorerViewModel = AddonNodeExplorerViewModel;
            if (explorerViewModel is null)
            {
                return;
            }

            var save = new AddonRootSave
            {
                Nodes = [.. explorerViewModel.SelectedNodes.Select(node => node.CreateSave())]
            };
            await Task.Run(() => File.WriteAllText(filePath, AddonRoot.Serialize(save)));

            await _dialogService.ShowInfoAsync(Texts.SaveAddonRootFileSuccess.FormatNoThrow(filePath));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during exporting .addonroot file: {FilePath}", filePath);
            await _dialogService.ShowExceptionAsync(ex);
        }
    }

    private async Task ShowItemsRandomSelectedAsync(int count)
    {
        if (count == 0)
        {
            await _dialogService.ShowInfoAsync(Texts.NoItemRandomSelected);
        }
        else
        {
            await _dialogService.ShowInfoAsync(Texts.ItemsRandomSelectedWithCount.FormatNoThrow(count));
        }
    }

    private async Task DeleteRedundantVpkFilesAsync(bool selectedItems = false)
    {
        var addonRoot = AddonRoot;
        if (addonRoot is null)
        {
            return;
        }

        IEnumerable<AddonNode>? targetAddons = selectedItems ?
            AddonNodeExplorerViewModel?.SelectedNodes?.SelectMany(addon => addon.GetSelfAndDescendants()) :
            addonRoot.GetDescendants();
        if (targetAddons is null)
        {
            return;
        }

        try
        {
            var report = WorkshopVpkAddon.DeleteRedundantVpkFilesReport.Combine(
                targetAddons.OfType<WorkshopVpkAddon>().Select(addon => addon.RequestDeleteRedundantVpkFiles()));
            bool confirm = await RedundantVpkFileDialogs.ConfirmDeleteAsync(_dialogService, report);
            if (!confirm)
            {
                return;
            }
            report.Execute();
            await RedundantVpkFileDialogs.ShowDeleteSuccessAsync(_dialogService, report);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowExceptionAsync(ex);
        }
    }

    private async Task DeleteAddonsWithMissingFilesAsync(bool selectedItems = false)
    {
        var explorerViewModel = AddonNodeExplorerViewModel;
        if (explorerViewModel is null)
        {
            return;
        }

        IEnumerable<AddonNode> targetAddons = selectedItems ?
            explorerViewModel.SelectedNodes.SelectMany(addon => addon.GetSelfAndDescendants()) :
            explorerViewModel.AddonRoot.GetDescendants();
        var addonsToDelete = new List<AddonNode>();
        foreach (var addon in targetAddons)
        {
            if (addon.RequireFile)
            {
                bool filePresent = true;
                var filePath = addon.FullFilePath;
                try
                {
                    filePresent = FileSystemUtils.Exists(filePath);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception occurred during getting the existence of the file system entry: {Path}", filePath);
                }
                if (!filePresent)
                {
                    addonsToDelete.Add(addon);
                }
            }
        }
        int count = addonsToDelete.Count;
        if (count == 0)
        {
            await _dialogService.ShowInfoAsync(Texts.AddonsWithMissingFilesNotFound);
            return;
        }
        bool confirm = await _dialogService.ConfirmAsync(
            Texts.ConfirmDeleteAddonsWithMissingFilesWithCount.FormatNoThrow(count));
        if (!confirm)
        {
            return;
        }
        var deletionTasks = new (Task, string)[count];
        for (int i = 0; i < count; i++)
        {
            var addon = addonsToDelete[i];
            var nodePath = addon.NodePath;
            deletionTasks[i] = (addon.DestroyAsync(), nodePath);
        }
        _windowService.ShowDeletionProgress(deletionTasks);
    }

    private void OnAddonRootNewDownloadItem(IDownloadItem downloadItem)
    {
        _downloadItemListViewModel.Add(downloadItem);
    }

    private void OnAddonRootPushed()
    {
        LastPushDateTime = DateTime.Now;
    }

    private void OnAddonNodeJumpMessage(object recipient, AddonNodeJumpMessage message)
    {
        var explorerViewModel = AddonNodeExplorerViewModel;
        if (explorerViewModel is null)
        {
            return;
        }

        explorerViewModel.GotoNode(message.Target);
        _windowService.ShowLibraryPage();
    }

    private void OnAppSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppSettings.GamePath):
                if (_addonRoot is not null)
                {
                    _addonRoot.GamePath = _appSettings.GamePath;
                }
                break;
            case nameof(AppSettings.IgnoreHalfLife2FilesForVpkAddonConflicts):
                UpdateVpkAddonConflictCheckSettings();
                break;
            case nameof(AppSettings.AddonNodeSortMethod):
            case nameof(AppSettings.IsAddonNodeAscendingOrder):
            case nameof(AppSettings.AddonNodeListItemViewKind):
                if (!_isSyncingExplorerSettings)
                {
                    SyncExplorerSettingsFromAppSettings(AddonNodeExplorerViewModel);
                }
                break;
        }
    }

    private void OnExplorerViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AddonNodeExplorerViewModel.HasSelection):
                UpdateHasSelection();
                break;
            case nameof(AddonNodeExplorerViewModel.SortMethod):
                if (!_isSyncingExplorerSettings && sender is AddonNodeExplorerViewModel explorer1)
                {
                    _isSyncingExplorerSettings = true;
                    try
                    {
                        _appSettings.AddonNodeSortMethod = explorer1.SortMethod;
                    }
                    finally
                    {
                        _isSyncingExplorerSettings = false;
                    }
                }
                break;
            case nameof(AddonNodeExplorerViewModel.IsAscendingOrder):
                if (!_isSyncingExplorerSettings && sender is AddonNodeExplorerViewModel explorer2)
                {
                    _isSyncingExplorerSettings = true;
                    try
                    {
                        _appSettings.IsAddonNodeAscendingOrder = explorer2.IsAscendingOrder;
                    }
                    finally
                    {
                        _isSyncingExplorerSettings = false;
                    }
                }
                break;
            case nameof(AddonNodeExplorerViewModel.ListItemViewKind):
                if (!_isSyncingExplorerSettings && sender is AddonNodeExplorerViewModel explorer3)
                {
                    _isSyncingExplorerSettings = true;
                    try
                    {
                        _appSettings.AddonNodeListItemViewKind = explorer3.ListItemViewKind;
                    }
                    finally
                    {
                        _isSyncingExplorerSettings = false;
                    }
                }
                break;
        }
    }

    private void ObserveExplorerViewModel(AddonNodeExplorerViewModel? explorerViewModel)
    {
        if (_observedExplorerViewModel is not null)
        {
            _observedExplorerViewModel.PropertyChanged -= OnExplorerViewModelPropertyChanged;
            _observedExplorerViewModel = null;
        }

        if (explorerViewModel is not null)
        {
            _observedExplorerViewModel = explorerViewModel;
            explorerViewModel.PropertyChanged += OnExplorerViewModelPropertyChanged;
        }
    }

    private void SyncExplorerSettingsFromAppSettings(AddonNodeExplorerViewModel? explorerViewModel)
    {
        if (explorerViewModel is null)
        {
            return;
        }

        _isSyncingExplorerSettings = true;
        try
        {
            explorerViewModel.SortMethod = _appSettings.AddonNodeSortMethod;
            explorerViewModel.IsAscendingOrder = _appSettings.IsAddonNodeAscendingOrder;
            explorerViewModel.ListItemViewKind = _appSettings.AddonNodeListItemViewKind;
        }
        finally
        {
            _isSyncingExplorerSettings = false;
        }
    }

    private void UpdateVpkAddonConflictCheckSettings()
    {
        bool ignoreHl2 = _appSettings.IgnoreHalfLife2FilesForVpkAddonConflicts;
        _vpkAddonConflictCheckSettings = new()
        {
            IgnoringFileSet = new VpkAddonConflictIgnoringFileSet([], [
                () => _appSettings.WaitCustomVpkAddonConflictIgnoringFiles(),
                () => ignoreHl2 ? CommonVpkAddonConflictIgnoringFiles.HalfLife2 : null
            ])
        };
    }

    private void UpdateHasSelection()
    {
        bool hasSelection = AddonNodeExplorerViewModel?.HasSelection ?? false;
        if (SetProperty(ref _hasSelection, hasSelection, nameof(HasSelection)))
        {
            NotifySelectionCommandsCanExecuteChanged();
        }
    }

    private void UpdateDerivedState()
    {
        SetProperty(ref _isEmptyDirectory, _addonRoot is null && !IsOpeningDirectory, nameof(IsEmptyDirectory));
        SetProperty(ref _titleExtraInfo, _addonRoot is null ? "" : " - " + _addonRoot.DirectoryPath, nameof(TitleExtraInfo));

        NotifyAddonRootCommandsCanExecuteChanged();
    }

    private void NotifyAddonRootCommandsCanExecuteChanged()
    {
        CloseDirectoryCommand.NotifyCanExecuteChanged();
        ImportCommand.NotifyCanExecuteChanged();
        ImportAddonRootFileCommand.NotifyCanExecuteChanged();
        OpenProblemListWindowCommand.NotifyCanExecuteChanged();
        OpenTagManagerWindowCommand.NotifyCanExecuteChanged();
        OpenVpkConflictListWindowCommand.NotifyCanExecuteChanged();
        OpenReplacementOverviewWindowCommand.NotifyCanExecuteChanged();
        OpenFileCleanerWindowCommand.NotifyCanExecuteChanged();
        OpenAddonNameAutoSetterWindowCommand.NotifyCanExecuteChanged();
        OpenFileNameFixerWindowCommand.NotifyCanExecuteChanged();
        PushCommand.NotifyCanExecuteChanged();
        CheckCommand.NotifyCanExecuteChanged();
        ClearCachesCommand.NotifyCanExecuteChanged();
        RandomlySelectCommand.NotifyCanExecuteChanged();
        DeleteRedundantVpkFilesCommand.NotifyCanExecuteChanged();
        DeleteAddonsWithMissingFilesCommand.NotifyCanExecuteChanged();
        RunGameCommand.NotifyCanExecuteChanged();
        NotifySelectionCommandsCanExecuteChanged();
    }

    private void NotifySelectionCommandsCanExecuteChanged()
    {
        RandomlySelectForSelectedItemsCommand.NotifyCanExecuteChanged();
        DeleteRedundantVpkFilesForSelectedItemsCommand.NotifyCanExecuteChanged();
        ExportSelectedItemsAsAddonRootFile.NotifyCanExecuteChanged();
        DeleteAddonsWithMissingFilesForSelectedItemsCommand.NotifyCanExecuteChanged();
    }
}
