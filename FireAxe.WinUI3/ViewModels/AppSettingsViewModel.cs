// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\AppSettingsViewModel.cs
// ReactiveUI (IActivatableViewModel/WhenActivated/WhenAnyValue/ReactiveCommand) ->
//   FireAxe.ViewModels.ViewModelBase + [ObservableProperty]/[RelayCommand] + IViewModelLifecycle.
// Interaction<Unit,string?> ChooseGamePathDirectory / <string,bool> ConfirmFoundGamePath /
// <Unit,Unit> ReportGamePathNotFound -> IPickerService.PickFolderAsync + IDialogService.
//   Titles and texts are the ones AppSettingsWindow.axaml.cs passed to CommonMessageBoxes
//   (Confirm -> Texts.Confirm, ShowInfo -> title "").
// NOTE: the three numeric settings are exposed as strings because the Avalonia view bound TextBoxes
//       to them; a non-numeric input still throws ArgumentException(Texts.ValueMustBeInteger), which
//       the WinUI view has to translate into a validation error (same contract as before).
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FireAxe.Resources;
using FireAxe.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;

namespace FireAxe.ViewModels;

public sealed partial class AppSettingsViewModel : ViewModelBase, IViewModelLifecycle
{
    private readonly AppSettings _settings;
    private readonly MainWindowViewModel _mainWindowViewModel;
    private readonly IPickerService? _pickerService;
    private readonly IDialogService? _dialogService;

    private readonly IReadOnlyList<string?> _languageItemsSource;

    private bool _isActive = false;

    public AppSettingsViewModel(AppSettings settings, MainWindowViewModel mainWindowViewModel)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(mainWindowViewModel);

        _settings = settings;
        _mainWindowViewModel = mainWindowViewModel;

        _pickerService = AppServices.TryGetService<IPickerService>();
        _dialogService = AppServices.TryGetService<IDialogService>();

        _languageItemsSource = [null, .. CultureManager.SupportedCultureStrings];

        ShowSettingsFileCommand = new RelayCommand(ShowSettingsFile);
        SelectGamePathCommand = new AsyncRelayCommand(SelectGamePathAsync);
        FindGamePathCommand = new AsyncRelayCommand(FindGamePathAsync);
        OpenCustomVpkAddonConflictIgnoringFilesDirectory = new RelayCommand(OpenCustomVpkAddonConflictIgnoringFilesDirectoryExec);
        OpenBackupDirectoryCommand = new RelayCommand(OpenBackupDirectory, () => AddonRootNotNull);
    }

    public AppSettings Settings => _settings;

    public IEnumerable<string?> LanguageItemsSource => _languageItemsSource;

    /// <summary>True while a directory is open; gates <see cref="OpenBackupDirectoryCommand"/>.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenBackupDirectoryCommand))]
    public partial bool AddonRootNotNull { get; private set; }

    public string MaxRetainedBackupFileCount
    {
        get => _settings.MaxRetainedBackupFileCount.ToString();
        set
        {
            if (!int.TryParse(value, out int count))
            {
                throw new ArgumentException(Texts.ValueMustBeInteger);
            }

            _settings.MaxRetainedBackupFileCount = count;
        }
    }

    public string FileBackupIntervalMinutes
    {
        get => _settings.FileBackupIntervalMinutes.ToString();
        set
        {
            if (!int.TryParse(value, out int minutes))
            {
                throw new ArgumentException(Texts.ValueMustBeInteger);
            }

            _settings.FileBackupIntervalMinutes = minutes;
        }
    }

    public string DownloadChunkCount
    {
        get => _settings.DownloadChunkCount.ToString();
        set
        {
            if (!int.TryParse(value, out int count))
            {
                throw new ArgumentException(Texts.ValueMustBeInteger);
            }

            _settings.DownloadChunkCount = count;
        }
    }

    public string MaxConcurrentDownloads
    {
        get => _settings.MaxConcurrentDownloads.ToString();
        set
        {
            if (!int.TryParse(value, out int count))
            {
                throw new ArgumentException(Texts.ValueMustBeInteger);
            }

            _settings.MaxConcurrentDownloads = count;
        }
    }

    public IRelayCommand ShowSettingsFileCommand { get; }

    public IAsyncRelayCommand SelectGamePathCommand { get; }

    public IAsyncRelayCommand FindGamePathCommand { get; }

    /// <summary>Name kept as-is because the Avalonia view bound it without a "Command" suffix.</summary>
    public IRelayCommand OpenCustomVpkAddonConflictIgnoringFilesDirectory { get; }

    public IRelayCommand OpenBackupDirectoryCommand { get; }

    public void Activate()
    {
        if (_isActive)
        {
            return;
        }
        _isActive = true;

        _mainWindowViewModel.PropertyChanged += OnMainWindowViewModelPropertyChanged;
        _settings.PropertyChanged += OnSettingsPropertyChanged;

        // WhenAnyValue pushed the current value on subscribe; the event handler only fires on change.
        AddonRootNotNull = _mainWindowViewModel.AddonRoot is not null;
    }

    public void Deactivate()
    {
        if (!_isActive)
        {
            return;
        }
        _isActive = false;

        _mainWindowViewModel.PropertyChanged -= OnMainWindowViewModelPropertyChanged;
        _settings.PropertyChanged -= OnSettingsPropertyChanged;

        AddonRootNotNull = false;
    }

    private void ShowSettingsFile()
    {
        Utils.ShowInFileExplorer(_settings.SettingsFilePath);
    }

    private async Task SelectGamePathAsync()
    {
        if (_pickerService is null)
        {
            return;
        }

        var path = await _pickerService.PickFolderAsync(new FolderPickerOptions { Title = Texts.SelectGamePath });
        if (path != null)
        {
            _settings.GamePath = path;
        }
    }

    private async Task FindGamePathAsync()
    {
        var gamePath = GamePathUtils.TryFind();
        if (gamePath is null)
        {
            if (_dialogService is not null)
            {
                await _dialogService.ShowInfoAsync(Texts.GamePathNotFoundMessage);
            }
            return;
        }
        if (_dialogService is null)
        {
            return;
        }
        bool confirm = await _dialogService.ConfirmAsync(
            Texts.ConfirmFoundGamePathMessage.FormatNoThrow(gamePath), Texts.Confirm);
        if (!confirm)
        {
            return;
        }
        _settings.GamePath = gamePath;
    }

    private void OpenCustomVpkAddonConflictIgnoringFilesDirectoryExec()
    {
        var dirPath = _settings.CustomVpkAddonConflictIgnoringFilesDirectoryPath;
        try
        {
            Directory.CreateDirectory(dirPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during creating the directory: {Path}", dirPath);
            return;
        }
        try
        {
            var readmeFilePath = Path.Join(dirPath, "readme.txt");
            if (!FileSystemUtils.Exists(readmeFilePath))
            {
                using var sourceStream = File.OpenRead(Path.Join(AppGlobal.ExportedAssetsDirectoryName, "CustomVpkAddonConflictIgnoringFiles_ReadMe.asset"));
                using var targetStream = File.Create(readmeFilePath);
                sourceStream.CopyTo(targetStream);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during creating CustomVpkAddonConflictIgnoringFiles readme file.");
        }
        Utils.ShowInFileExplorer(dirPath, openDir: true);
    }

    private void OpenBackupDirectory()
    {
        if (_mainWindowViewModel.AddonRoot?.BackupDirectoryPath is { } path)
        {
            Utils.ShowInFileExplorer(path, openDir: true);
        }
    }

    private void OnMainWindowViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.AddonRoot) or "")
        {
            AddonRootNotNull = _mainWindowViewModel.AddonRoot is not null;
        }
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The Avalonia version re-raised the string proxies from WhenAnyValue subscriptions.
        switch (e.PropertyName)
        {
            case nameof(AppSettings.MaxRetainedBackupFileCount):
                OnPropertyChanged(nameof(MaxRetainedBackupFileCount));
                break;
            case nameof(AppSettings.FileBackupIntervalMinutes):
                OnPropertyChanged(nameof(FileBackupIntervalMinutes));
                break;
            case nameof(AppSettings.DownloadChunkCount):
                OnPropertyChanged(nameof(DownloadChunkCount));
                break;
            case nameof(AppSettings.MaxConcurrentDownloads):
                OnPropertyChanged(nameof(MaxConcurrentDownloads));
                break;
        }
    }
}
