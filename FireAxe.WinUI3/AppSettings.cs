// Ported from FireAxe_Alt\FireAxe.GUI\AppSettings.cs
// Changes for P1 (see docs/phase-notes/P1-abstraction.md):
//   * App dependency replaced by documentDirectoryPath + IAppThemeService (no Avalonia in the VM layer).
//   * Rx `WhenAnyValue(...).Throttle(300ms)` replaced by a BCL debounce in ScheduleDownloadSettingsSync.
//   * `ReactiveCommand LoadCustomVpkAddonConflictIgnoringFilesCommand` replaced by a plain async method.
// Everything else (property set, JSON contract, validation) is unchanged.
using FireAxe.Resources;
using FireAxe.Services;
using FireAxe.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Serilog;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FireAxe;

[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
public sealed class AppSettings : ObservableObject, ISaveable, IDisposable
{
    public const string SettingsFileName = "Settings.json";

    public const string CustomVpkAddonConflictIgnoringFilesDirectoryName = "CustomVpkAddonConflictIgnoringFiles";

    /// <summary>How long the download-related settings settle before being pushed to the download service.</summary>
    private static readonly TimeSpan DownloadSettingsSyncDelay = TimeSpan.FromMilliseconds(300);

    private static readonly JsonSerializerSettings s_jsonSettings = new()
    {
        Formatting = Formatting.Indented,
        Converters =
        {
            new StringEnumConverter()
        }
    };

    private bool _disposed = false;

    private readonly IAppThemeService _themeService;

    private readonly SynchronizationContext? _syncContext;

    private readonly string _settingsFilePath;
    private readonly string _documentDirectoryPath;

    private CancellationTokenSource? _downloadSettingsSyncCts = null;

    private AddonNodeSortMethod _addonNodeSortMethod = AddonNodeSortMethod.None;
    private bool _isAddonNodeAscendingOrder = true;
    private AddonNodeListItemViewKind _addonNodeListItemViewKind = AddonNodeListItemViewKind.MediumTile;
    private string? _lastOpenDirectory = null;
    private string? _language = null;
    private string _gamePath = "";
    private bool _ignoreHalfLife2FilesForVpkAddonConflicts = true;
    private bool _isAutoUpdateWorkshopItem = true;
    private string? _suppressedUpdateRequestVersion = null;
    private bool _isAutoCheckUpdateEnabled = true;
    private string _updateCheckRepository = AppGlobal.DefaultUpdateCheckRepository;
    private bool _isAutoDetectWorkshopItemLinkInClipboard = true;
    private bool _isAutoRedownload = false;
    private Uri? _webProxyUri = null;
    private bool _isFileBackupEnabled = true;
    private int _maxRetainedBackupFileCount = 25;
    private int _fileBackupIntervalMinutes = 30;
    private string _customLaunchOptions = "";
    private bool _isParallelDownloadEnabled = true;
    private int _downloadChunkCount = 4;
    private int _maxConcurrentDownloads = 3;

    private FrozenSet<string>? _customVpkAddonConflictIgnoringFiles = null;
    private readonly object _customVpkAddonConflictIgnoringFilesLock = new();
    private bool _isLoadingCustomVpkAddonConflictIgnoringFiles = false;

    /// <param name="documentDirectoryPath">Usually <c>%MyDocuments%\FireAxe</c>.</param>
    /// <param name="themeService">Reports and applies the colour theme (Avalonia's <c>Application.RequestedThemeVariant</c>).</param>
    public AppSettings(string documentDirectoryPath, IAppThemeService themeService)
    {
        ArgumentNullException.ThrowIfNull(documentDirectoryPath);
        ArgumentNullException.ThrowIfNull(themeService);

        _documentDirectoryPath = documentDirectoryPath;
        _themeService = themeService;
        _settingsFilePath = Path.Join(documentDirectoryPath, SettingsFileName);
        _syncContext = SynchronizationContext.Current;

        _themeService.ColorThemeChanged += OnColorThemeChanged;

        LoadFile();
        _ = LoadCustomVpkAddonConflictIgnoringFilesAsync();
    }

    public bool RequestSave { get; set; } = true;

    /// <summary>
    /// The download service to keep in sync with the download settings. Set once after construction.
    /// </summary>
    public DownloadService? DownloadService
    {
        get => field;
        set
        {
            field = value;
            if (value is not null)
            {
                value.Settings = this.GetDownloadServiceSettings();
            }
        }
    }

    public string SettingsFilePath => _settingsFilePath;

    public string DocumentDirectoryPath => _documentDirectoryPath;

    public string CustomVpkAddonConflictIgnoringFilesDirectoryPath => Path.Join(_documentDirectoryPath, CustomVpkAddonConflictIgnoringFilesDirectoryName);

    public IReadOnlySet<string>? CustomVpkAddonConflictIgnoringFiles
    {
        get
        {
            IReadOnlySet<string>? result = null;
            if (Monitor.TryEnter(_customVpkAddonConflictIgnoringFilesLock))
            {
                result = _customVpkAddonConflictIgnoringFiles;
                Monitor.Exit(_customVpkAddonConflictIgnoringFilesLock);
            }
            return result;
        }
    }

    public bool IsLoadingCustomVpkAddonConflictIgnoringFiles
    {
        get => _isLoadingCustomVpkAddonConflictIgnoringFiles;
        private set => NotifyAndSetIfChanged(ref _isLoadingCustomVpkAddonConflictIgnoringFiles, value);
    }

    [JsonProperty]
    public AddonNodeSortMethod AddonNodeSortMethod
    {
        get => _addonNodeSortMethod;
        set
        {
            if (NotifyAndSetIfChanged(ref _addonNodeSortMethod, value))
            {
                RequestSave = true;
            }
        }
    }

    [JsonProperty]
    public bool IsAddonNodeAscendingOrder
    {
        get => _isAddonNodeAscendingOrder;
        set
        {
            if (NotifyAndSetIfChanged(ref _isAddonNodeAscendingOrder, value))
            {
                RequestSave = true;
            }
        }
    }

    [JsonProperty]
    public AddonNodeListItemViewKind AddonNodeListItemViewKind
    {
        get => _addonNodeListItemViewKind;
        set
        {
            if (NotifyAndSetIfChanged(ref _addonNodeListItemViewKind, value))
            {
                RequestSave = true;
            }
        }
    }

    [JsonProperty]
    public string? LastOpenDirectory
    {
        get => _lastOpenDirectory;
        set
        {
            if (value == _lastOpenDirectory)
            {
                return;
            }
            if (value != null)
            {
                if (!FileSystemUtils.IsValidPath(value) || !Path.IsPathRooted(value))
                {
                    throw new ArgumentException("The path is invalid.");
                }
            }
            _lastOpenDirectory = value;
            NotifyChanged();
            RequestSave = true;
        }
    }

    [JsonProperty]
    public string? Language
    {
        get => _language;
        set
        {
            if (value is not null && !CultureManager.SupportedCultureStrings.Contains(value))
            {
                value = null;
            }
            if (value == _language)
            {
                return;
            }

            CultureManager.Instance.SetCurrentCulture(value);
            _language = value;
            NotifyChanged();
            RequestSave = true;
        }
    }

    [JsonProperty]
    public AppColorTheme ColorTheme
    {
        get => _themeService.ColorTheme;
        set
        {
            if (_themeService.ColorTheme == value)
            {
                return;
            }
            _themeService.ColorTheme = value;
            RequestSave = true;
        }
    }

    [JsonProperty]
    public double? MainWindowWidth
    {
        get;
        set
        {
            if (value is not null)
            {
                value = Math.Max(0, value.Value);
            }
            if (value == field)
            {
                return;
            }

            field = value;
            NotifyChanged();

            RequestSave = true;
        }
    } = null;

    [JsonProperty]
    public double? MainWindowHeight
    {
        get;
        set
        {
            if (value is not null)
            {
                value = Math.Max(0, value.Value);
            }
            if (value == field)
            {
                return;
            }

            field = value;
            NotifyChanged();

            RequestSave = true;
        }
    } = null;

    [JsonProperty]
    public bool IsMainWindowMaximized
    {
        get;
        set
        {
            if (NotifyAndSetIfChanged(ref field, value))
            {
                RequestSave = true;
            }
        }
    } = false;

    [JsonProperty]
    public string GamePath
    {
        get => _gamePath;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Length > 0)
            {
                if (!FileSystemUtils.IsValidPath(value) || !Path.IsPathRooted(value))
                {
                    throw new ArgumentException(Texts.InvalidFilePath);
                }
                value = FileSystemUtils.NormalizePath(value);
            }
            if (value == _gamePath)
            {
                return;
            }

            _gamePath = value;
            NotifyChanged();
            RequestSave = true;
        }
    }

    [JsonProperty]
    public bool IgnoreHalfLife2FilesForVpkAddonConflicts
    {
        get => _ignoreHalfLife2FilesForVpkAddonConflicts;
        set
        {
            if (NotifyAndSetIfChanged(ref _ignoreHalfLife2FilesForVpkAddonConflicts, value))
            {
                RequestSave = true;
            }
        }
    }

    [JsonProperty]
    public bool IsAutoUpdateWorkshopItem
    {
        get => _isAutoUpdateWorkshopItem;
        set
        {
            if (NotifyAndSetIfChanged(ref _isAutoUpdateWorkshopItem, value))
            {
                RequestSave = true;
            }
        }
    }

    [JsonProperty]
    public string? SuppressedUpdateRequestVersion
    {
        get => _suppressedUpdateRequestVersion;
        set
        {
            if (NotifyAndSetIfChanged(ref _suppressedUpdateRequestVersion, value))
            {
                RequestSave = true;
            }
        }
    }

    [JsonProperty]
    public bool IsAutoCheckUpdateEnabled
    {
        get => _isAutoCheckUpdateEnabled;
        set
        {
            if (NotifyAndSetIfChanged(ref _isAutoCheckUpdateEnabled, value))
            {
                RequestSave = true;
            }
        }
    }

    [AllowNull]
    [JsonProperty]
    public string UpdateCheckRepository
    {
        get => _updateCheckRepository;
        set
        {
            value = NormalizeRepository(value);
            if (value == _updateCheckRepository)
            {
                return;
            }

            _updateCheckRepository = value;
            NotifyChanged();
            RequestSave = true;
        }
    }

    // Accepts "owner/name", and also tolerates a pasted GitHub url or stray slashes and whitespace.
    private static string NormalizeRepository(string? value)
    {
        value ??= "";
        value = value.Trim();

        const string UrlPrefix = "https://github.com/";
        const string HttpUrlPrefix = "http://github.com/";
        if (value.StartsWith(UrlPrefix, StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(UrlPrefix.Length);
        }
        else if (value.StartsWith(HttpUrlPrefix, StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(HttpUrlPrefix.Length);
        }

        value = value.Trim('/');

        var segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return AppGlobal.DefaultUpdateCheckRepository;
        }
        return $"{segments[0]}/{segments[1]}";
    }

    [JsonProperty]
    public bool IsAutoDetectWorkshopItemLinkInClipboard
    {
        get => _isAutoDetectWorkshopItemLinkInClipboard;
        set
        {
            if (NotifyAndSetIfChanged(ref _isAutoDetectWorkshopItemLinkInClipboard, value))
            {
                RequestSave = true;
            }
        }
    }

    [JsonProperty]
    public bool IsAutoRedownload
    {
        get => _isAutoRedownload;
        set
        {
            if (NotifyAndSetIfChanged(ref _isAutoRedownload, value))
            {
                RequestSave = true;
            }
        }
    }

    /// <summary>
    /// Launch options to use when running the game. When empty, the options configured in Steam are used.
    /// </summary>
    [AllowNull]
    [JsonProperty]
    public string CustomLaunchOptions
    {
        get => _customLaunchOptions;
        set
        {
            value ??= "";
            if (NotifyAndSetIfChanged(ref _customLaunchOptions, value))
            {
                RequestSave = true;
            }
        }
    }

    /// <summary>
    /// Whether a single download is split into multiple parallel chunks.
    /// </summary>
    [JsonProperty]
    public bool IsParallelDownloadEnabled
    {
        get => _isParallelDownloadEnabled;
        set
        {
            if (NotifyAndSetIfChanged(ref _isParallelDownloadEnabled, value))
            {
                RequestSave = true;
                ScheduleDownloadSettingsSync();
            }
        }
    }

    /// <summary>
    /// The number of parallel chunks per download when <see cref="IsParallelDownloadEnabled"/> is on.
    /// </summary>
    [JsonProperty]
    public int DownloadChunkCount
    {
        get => _downloadChunkCount;
        set
        {
            value = Math.Clamp(value, 1, 16);
            if (NotifyAndSetIfChanged(ref _downloadChunkCount, value))
            {
                RequestSave = true;
                ScheduleDownloadSettingsSync();
            }
        }
    }

    /// <summary>
    /// The maximum number of downloads that run at the same time.
    /// </summary>
    [JsonProperty]
    public int MaxConcurrentDownloads
    {
        get => _maxConcurrentDownloads;
        set
        {
            value = Math.Clamp(value, 1, 16);
            if (NotifyAndSetIfChanged(ref _maxConcurrentDownloads, value))
            {
                RequestSave = true;
                ScheduleDownloadSettingsSync();
            }
        }
    }

    [AllowNull]
    [JsonProperty]
    public string WebProxyAddress
    {
        get => _webProxyUri?.OriginalString ?? "";
        set
        {
            value ??= "";
            if (value == WebProxyAddress)
            {
                return;
            }

            Uri? uri = null;
            if (value.Length > 0)
            {
                try
                {
                    uri = new Uri(value);
                }
                catch (UriFormatException)
                {
                    throw new ArgumentException(Texts.InvalidUri);
                }
            }

            _webProxyUri = uri;
            NotifyChanged();

            RequestSave = true;
            ScheduleDownloadSettingsSync();
        }
    }

    [AllowNull]
    [JsonProperty]
    public string WebProxyCredentialUserName
    {
        get;
        set
        {
            value ??= "";
            if (NotifyAndSetIfChanged(ref field, value))
            {
                RequestSave = true;
                ScheduleDownloadSettingsSync();
            }
        }
    } = "";

    [AllowNull]
    [JsonProperty]
    public string WebProxyCredentialPassword
    {
        get;
        set
        {
            value ??= "";
            if (NotifyAndSetIfChanged(ref field, value))
            {
                RequestSave = true;
                ScheduleDownloadSettingsSync();
            }
        }
    } = "";

    [JsonProperty]
    public bool IsFileBackupEnabled
    {
        get => _isFileBackupEnabled;
        set
        {
            if (NotifyAndSetIfChanged(ref _isFileBackupEnabled, value))
            {
                RequestSave = true;
            }
        }
    }

    [JsonProperty]
    public int MaxRetainedBackupFileCount
    {
        get => _maxRetainedBackupFileCount;
        set
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (value == _maxRetainedBackupFileCount)
            {
                return;
            }

            _maxRetainedBackupFileCount = value;
            NotifyChanged();

            RequestSave = true;
        }
    }

    [JsonProperty]
    public int FileBackupIntervalMinutes
    {
        get => _fileBackupIntervalMinutes;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (value == _fileBackupIntervalMinutes)
            {
                return;
            }

            _fileBackupIntervalMinutes = value;
            NotifyChanged();

            RequestSave = true;
        }
    }

    [AllowNull]
    [JsonProperty]
    public string OpenDirectoryCustomProcessFileName
    {
        get;
        set
        {
            value ??= "";
            if (NotifyAndSetIfChanged(ref field, value))
            {
                Utils.OpenDirectoryCustomProcessFileName = value;
                RequestSave = true;
            }
        }
    } = "";

    [AllowNull]
    [JsonProperty]
    public string OpenDirectoryCustomProcessArguments
    {
        get;
        set
        {
            value ??= "";
            if (NotifyAndSetIfChanged(ref field, value))
            {
                Utils.OpenDirectoryCustomProcessArguments = value;
                RequestSave = true;
            }
        }
    } = "";

    [AllowNull]
    [JsonProperty]
    public string ShowFileCustomProcessFileName
    {
        get;
        set
        {
            value ??= "";
            if (NotifyAndSetIfChanged(ref field, value))
            {
                Utils.ShowFileCustomProcessFileName = value;
                RequestSave = true;
            }
        }
    } = "";

    [AllowNull]
    [JsonProperty]
    public string ShowFileCustomProcessArguments
    {
        get;
        set
        {
            value ??= "";
            if (NotifyAndSetIfChanged(ref field, value))
            {
                Utils.ShowFileCustomProcessArguments = value;
                RequestSave = true;
            }
        }
    } = "";

    /// <summary>
    /// Loads the custom VPK-conflict ignoring files. Returns the freshly loaded set
    /// (was <c>LoadCustomVpkAddonConflictIgnoringFilesCommand</c> in the Avalonia version).
    /// </summary>
    public async Task<IReadOnlySet<string>?> LoadCustomVpkAddonConflictIgnoringFilesAsync()
    {
        if (IsLoadingCustomVpkAddonConflictIgnoringFiles)
        {
            return await Task.Run(WaitCustomVpkAddonConflictIgnoringFiles);
        }

        IsLoadingCustomVpkAddonConflictIgnoringFiles = true;
        try
        {
            var dirPath = CustomVpkAddonConflictIgnoringFilesDirectoryPath;

            var oldValue = CustomVpkAddonConflictIgnoringFiles;
            try
            {
                await Task.Run(() =>
                {
                    lock (_customVpkAddonConflictIgnoringFilesLock)
                    {
                        _customVpkAddonConflictIgnoringFiles = null;
                        _customVpkAddonConflictIgnoringFiles = FrozenSet.ToFrozenSet(LoadCustomVpkAddonConflictIgnoringFiles(dirPath));
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during loading custom VPK addon conflict ignoring files.");
            }
            var newValue = CustomVpkAddonConflictIgnoringFiles;
            if (oldValue != newValue)
            {
                NotifyChanged(nameof(CustomVpkAddonConflictIgnoringFiles));
            }
            return newValue;
        }
        finally
        {
            IsLoadingCustomVpkAddonConflictIgnoringFiles = false;
        }
    }

    public IReadOnlySet<string>? WaitCustomVpkAddonConflictIgnoringFiles()
    {
        lock (_customVpkAddonConflictIgnoringFilesLock)
        {
            return _customVpkAddonConflictIgnoringFiles;
        }
    }

    private static IEnumerable<string> LoadCustomVpkAddonConflictIgnoringFiles(string dirPath)
    {
        if (!Directory.Exists(dirPath))
        {
            yield break;
        }
        foreach (var file in Directory.EnumerateFiles(dirPath))
        {
            if (!file.EndsWith(".txt"))
            {
                continue;
            }
            var fileName = Path.GetFileName(file);
            if (fileName == "readme.txt")
            {
                continue;
            }

            using (var reader = File.OpenText(file))
            {
                string? line;
                while ((line = reader.ReadLine()) is not null)
                {
                    line = line.Trim().Replace('\\', '/');
                    yield return line;
                }
            }
        }
    }

    public void LoadFile()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                JsonConvert.PopulateObject(File.ReadAllText(_settingsFilePath), this, s_jsonSettings);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during AppSettings.LoadFile.");
        }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(_settingsFilePath, JsonConvert.SerializeObject(this, s_jsonSettings));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during AppSettings.Save.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        _themeService.ColorThemeChanged -= OnColorThemeChanged;

        var cts = Interlocked.Exchange(ref _downloadSettingsSyncCts, null);
        if (cts is not null)
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    private void OnColorThemeChanged(object? sender, EventArgs e)
    {
        NotifyChanged(nameof(ColorTheme));
    }

    /// <summary>
    /// Debounces the "settings changed → download service settings" propagation.
    /// Replaces Rx's <c>WhenAnyValue(...).Throttle(300ms)</c>; the last change wins, as before.
    /// </summary>
    private void ScheduleDownloadSettingsSync()
    {
        var cts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _downloadSettingsSyncCts, cts);
        if (previous is not null)
        {
            previous.Cancel();
            previous.Dispose();
        }

        _ = SyncDownloadSettingsAsync(cts.Token);
    }

    private async Task SyncDownloadSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(DownloadSettingsSyncDelay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        void Apply()
        {
            if (_disposed)
            {
                return;
            }
            if (DownloadService is { } downloadService)
            {
                downloadService.Settings = this.GetDownloadServiceSettings();
            }
        }

        // The Avalonia version observed on the main-thread scheduler, so Keep the assignment there.
        if (_syncContext is { } syncContext)
        {
            syncContext.Post(_ => Apply(), null);
        }
        else
        {
            Apply();
        }
    }
}
