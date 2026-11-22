// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\WorkshopVpkFinderViewModel.cs
// ReactiveUI (ReactiveObject / ReactiveCommand / WhenAnyValue / WhenActivated / ViewModelActivator) ->
//   ViewModelBase + [ObservableProperty] / [RelayCommand] + IViewModelLifecycle.
// Interaction<string?, string?> ChooseDirectoryInteraction -> IPickerService.PickFolderAsync
//   (title and start directory taken from WorkshopVpkFinderWindow.axaml.cs / CommonMessageBoxes.ChooseDirectory).
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FireAxe.Resources;
using FireAxe.Services;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FireAxe.ViewModels;

public partial class WorkshopVpkFinderViewModel : ViewModelBase, IViewModelLifecycle, IDisposable
{
    public partial class StagedItem : ViewModelBase
    {
        private readonly WorkshopVpkFinderViewModel _finder;

        private CancellationTokenSource _cancellationTokenSource = new();

        internal StagedItem(ulong publishedFileId, WorkshopVpkFinderViewModel finder)
        {
            PublishedFileId = publishedFileId;
            _finder = finder;

            GetTitle();
        }

        public ulong PublishedFileId { get; }

        [ObservableProperty]
        public partial string? Title { get; private set; }

        [ObservableProperty]
        public partial bool IsGetTitleFailed { get; private set; }

        [RelayCommand]
        public void Remove()
        {
            CancelTasks();
            _finder._stagedItems.Remove(this);
        }

        [RelayCommand]
        public void OpenWebsite()
        {
            Utils.OpenWebsite($"https://steamcommunity.com/sharedfiles/filedetails/?id={PublishedFileId}");
        }

        private async void GetTitle()
        {
            try
            {
                var result = await PublishedFileUtils.GetPublishedFileDetailsAsync(PublishedFileId, _finder._httpClient, _cancellationTokenSource.Token);
                if (result.Details?.Title is { } title)
                {
                    Title = title;
                }
                else
                {
                    IsGetTitleFailed = true;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        internal void CancelTasks()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }
    }

    private bool _disposed = false;

    private readonly MainWindowViewModel _mainWindowViewModel;
    private readonly HttpClient _httpClient;

    private readonly ObservableCollection<StagedItem> _stagedItems = new();
    private readonly ReadOnlyObservableCollection<StagedItem> _stagedItemsReadOnly;

    private bool _isActive = false;

    private bool _isAddonRootPresent = false;

    public WorkshopVpkFinderViewModel(MainWindowViewModel mainWindowViewModel, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(mainWindowViewModel);
        ArgumentNullException.ThrowIfNull(httpClient);

        _mainWindowViewModel = mainWindowViewModel;
        _httpClient = httpClient;

        _stagedItemsReadOnly = new(_stagedItems);

        // Replaces the `WhenAnyValue(x => x.StagedItems.Count)` part of CreateCommand's can-execute pipeline.
        _stagedItems.CollectionChanged += OnStagedItemsChanged;
    }

    ~WorkshopVpkFinderViewModel()
    {
        Dispose(false);
    }

    public ReadOnlyObservableCollection<StagedItem> StagedItems => _stagedItemsReadOnly;

    /// <summary>
    /// Was the private <c>IsAddonRootPresent</c> observable property
    /// (kept private on purpose: it is not a binding target).
    /// </summary>
    private bool IsAddonRootPresent
    {
        get => _isAddonRootPresent;
        set
        {
            if (SetProperty(ref _isAddonRootPresent, value))
            {
                CreateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public event Action? CloseRequested = null;

    public void Activate()
    {
        if (_isActive)
        {
            return;
        }
        ThrowIfDisposed();
        _isActive = true;

        // WhenAnyValue emitted the current value on subscription; keep that behaviour.
        IsAddonRootPresent = _mainWindowViewModel.AddonRoot is not null;
        _mainWindowViewModel.PropertyChanged += OnMainWindowViewModelPropertyChanged;
    }

    public void Deactivate()
    {
        if (!_isActive)
        {
            return;
        }
        _isActive = false;

        _mainWindowViewModel.PropertyChanged -= OnMainWindowViewModelPropertyChanged;
        IsAddonRootPresent = false;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            foreach (var stagedItem in _stagedItems)
            {
                stagedItem.CancelTasks();
            }

            _disposed = true;
        }
    }

    private bool CanCreate()
    {
        return IsAddonRootPresent && _stagedItems.Count > 0;
    }

    /// <summary>
    /// Command form of the finder (was <c>ReactiveCommand.CreateFromTask(...)</c> whose body picked a folder
    /// through <c>ChooseDirectoryInteraction</c>).
    /// </summary>
    [RelayCommand]
    private async Task FindFromDirectoryAsync()
    {
        ThrowIfDisposed();

        string? workshopPath = null;
        if (_mainWindowViewModel.AddonRoot?.GamePath is { } gamePath && gamePath.Length > 0)
        {
            workshopPath = Path.Join(GamePathUtils.GetAddonsPath(gamePath), "workshop");
        }

        string? dirPath = null;
        var pickerService = AppServices.TryGetService<IPickerService>();
        if (pickerService is not null)
        {
            dirPath = await pickerService.PickFolderAsync(new FolderPickerOptions
            {
                Title = Texts.FindFromDirectory,
                StartDirectoryPath = workshopPath
            });
        }
        if (dirPath is null)
        {
            return;
        }
        FindFromDirectory(dirPath);
    }

    /// <summary>
    /// Was <c>ReactiveCommand.Create(() =&gt; { Create(); CloseRequested?.Invoke(); }, canExecute)</c>.
    /// The name is forced by the generator ("Async" is stripped to produce <c>CreateCommand</c>) because
    /// the public <see cref="Create"/> method has to keep its name.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCreate))]
    private Task CreateAsync()
    {
        Create();
        CloseRequested?.Invoke();
        return Task.CompletedTask;
    }

    public void Create()
    {
        ThrowIfDisposed();

        var addonRoot = _mainWindowViewModel.AddonRoot;
        if (addonRoot is null)
        {
            return;
        }
        var addonGroup = _mainWindowViewModel.AddonNodeExplorerViewModel?.CurrentGroup;

        foreach (var stagedItem in _stagedItems)
        {
            var addon = AddonNode.Create<WorkshopVpkAddon>(addonRoot, addonGroup);
            if (stagedItem.Title is { } title)
            {
                addon.Name = addon.Parent.GetUniqueChildName(title);
            }
            else
            {
                addon.Name = addon.Parent.GetUniqueChildName(Texts.UnnamedWorkshopAddon);
                addon.RequestAutoSetName = true;
            }
            addon.PublishedFileId = stagedItem.PublishedFileId;
        }
    }

    public int FindFromDirectory(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        ThrowIfDisposed();

        int count = 0;
        try
        {
            if (Directory.Exists(path))
            {
                foreach (string file in Directory.EnumerateFiles(path, "*.vpk", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (PublishedFileUtils.TryParsePublishedFileId(name, out var id) && !ContainsPublishedFileId(id))
                    {
                        _stagedItems.Add(new StagedItem(id, this));
                        count++;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Exception occurred during {nameof(WorkshopVpkFinderViewModel)}.{nameof(FindFromDirectory)}.");
        }
        return count;
    }

    private bool ContainsPublishedFileId(ulong id)
    {
        foreach (var stagedItem in _stagedItems)
        {
            if (stagedItem.PublishedFileId == id)
            {
                return true;
            }
        }
        return false;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }

    private void OnMainWindowViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.AddonRoot))
        {
            IsAddonRootPresent = _mainWindowViewModel.AddonRoot is not null;
        }
    }

    private void OnStagedItemsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        CreateCommand.NotifyCanExecuteChanged();
    }
}
