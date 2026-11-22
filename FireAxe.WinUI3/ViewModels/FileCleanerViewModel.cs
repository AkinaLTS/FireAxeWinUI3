// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\FileCleanerViewModel.cs
// ReactiveUI (ReactiveObject / ReactiveCommand / WhenAnyValue / WhenActivated / ViewModelActivator) ->
//   ViewModelBase + [ObservableProperty] / [RelayCommand] + IViewModelLifecycle.
// DynamicData (.ToObservableChangeSet().Transform().Bind(out ...), .Sum(...).ToProperty(...)) ->
//   rebuild of the ReadOnlyObservableCollection on CollectionChanged + manual total recomputation.
// Interaction<Exception,Unit> / Interaction<int,Unit> / Interaction<Unit,bool> ->
//   IDialogService.ShowExceptionAsync / ShowInfoAsync / ConfirmAsync (texts taken from FileCleanerWindow.axaml.cs).
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FireAxe.Resources;
using FireAxe.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FireAxe.ViewModels;

public partial class FileCleanerViewModel : ViewModelBase, IViewModelLifecycle, IValidity
{
    public partial class StagedItem : ViewModelBase
    {
        private readonly FileCleanerViewModel _host;

        internal StagedItem(FileCleanerViewModel host, string path, string? relativePath = null)
        {
            _host = host;
            Path = path;
            relativePath ??= FileSystemUtils.NormalizePath(System.IO.Path.GetRelativePath(_host.AddonRoot.DirectoryPath, path));
            RelativePath = relativePath;

            try
            {
                if (File.Exists(path))
                {
                    FileSize = new FileInfo(path).Length;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during getting the size of the file: {FilePath}", path);
            }
        }

        public string Path { get; }

        public string RelativePath { get; }

        public long? FileSize { get; } = null;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
        public partial bool IsDeleting { get; private set; }

        [ObservableProperty]
        public partial Exception? DeletionException { get; private set; }

        public async void Delete()
        {
            if (IsDeleting)
            {
                return;
            }

            DeletionException = null;
            IsDeleting = true;

            var path = Path;
            bool success = false;
            try
            {
                await Task.Run(() =>
                {
                    if (FileSystemUtils.Exists(path))
                    {
                        FileSystemUtils.MoveToRecycleBin(path);
                    }
                });
                success = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during deleting the file: {Path}", path);
                DeletionException = ex;
            }

            IsDeleting = false;

            if (success)
            {
                Remove();
            }
        }

        /// <summary>
        /// Command form of <see cref="Delete"/> (was <c>ReactiveCommand.Create(Delete, canExecute)</c>).
        /// The wrapper only exists because the command name is derived from the method name and
        /// <see cref="Delete"/> itself has to stay a public member.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanDelete))]
        private Task DeleteAsync()
        {
            Delete();
            return Task.CompletedTask;
        }

        private bool CanDelete() => !IsDeleting;

        /// <summary>
        /// Was <c>RetainCommand = ReactiveCommand.Create(Remove)</c>: the View binds "RetainCommand",
        /// so the command has to be generated from a method of that name.
        /// </summary>
        [RelayCommand]
        private void Retain()
        {
            Remove();
        }

        public void Remove()
        {
            _host._stagedItems.Remove(this);
        }

        [RelayCommand]
        private void ShowInFileExplorer()
        {
            Utils.ShowInFileExplorer(Path);
        }
    }

    public partial class SourceAddonViewModel : AddonNodeSimpleViewModel
    {
        private readonly FileCleanerViewModel _host;

        internal SourceAddonViewModel(FileCleanerViewModel host, Guid addonId) : base(host.AddonRoot, addonId)
        {
            _host = host;
        }

        [RelayCommand]
        public void Remove()
        {
            _host.RemoveSourceAddon(AddonId);
        }
    }

    private bool _isActive = false;

    private IDisposable? _invalidHandler = null;

    internal readonly ObservableCollection<StagedItem> _stagedItems = new();
    private readonly ReadOnlyObservableCollection<StagedItem> _stagedItemsReadOnly;

    private readonly ObservableCollection<Guid> _sourceAddonIds = new();
    private readonly ReadOnlyObservableCollection<Guid> _sourceAddonIdsReadOnly;
    private ReadOnlyObservableCollection<SourceAddonViewModel> _sourceAddonViewModels;

    private long _totalFileSize = 0;

    public FileCleanerViewModel(AddonRoot addonRoot)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);

        AddonRoot = addonRoot;

        IsValid = true;
        IsGlobalSearch = true;

        _stagedItemsReadOnly = new(_stagedItems);
        _sourceAddonIdsReadOnly = new(_sourceAddonIds);
        _sourceAddonViewModels = new(new ObservableCollection<SourceAddonViewModel>());

        // Replaces the DynamicData pipelines that used to run for the whole lifetime of the object
        // (they were created in the constructor, not inside WhenActivated).
        _stagedItems.CollectionChanged += OnStagedItemsChanged;
        _sourceAddonIds.CollectionChanged += OnSourceAddonIdsChanged;

        RebuildSourceAddonViewModels();
        UpdateTotalFileSize();
    }

    [ObservableProperty]
    public partial bool IsValid { get; private set; }

    public AddonRoot AddonRoot { get; }

    public ReadOnlyObservableCollection<StagedItem> StagedItems => _stagedItemsReadOnly;

    public ReadOnlyObservableCollection<Guid> SourceAddonIds => _sourceAddonIdsReadOnly;

    public ReadOnlyObservableCollection<SourceAddonViewModel> SourceAddonViewModels => _sourceAddonViewModels;

    public IEnumerable<AddonNode> ActualSourceAddons
    {
        get
        {
            var root = AddonRoot;

            if (IsGlobalSearch)
            {
                foreach (var addon in root.Nodes)
                {
                    yield return addon;
                }
                yield break;
            }

            foreach (var id in _sourceAddonIds)
            {
                if (root.TryGetNodeById(id, out var addon))
                {
                    yield return addon;
                }
            }
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindAllCleanableCommand))]
    [NotifyCanExecuteChangedFor(nameof(FindRedundantVpkFilesCommand))]
    [NotifyCanExecuteChangedFor(nameof(FindDownloadTempFilesCommand))]
    [NotifyCanExecuteChangedFor(nameof(FindEmptyDirectoriesCommand))]
    public partial bool IsGlobalSearch { get; set; }

    public long TotalFileSize => _totalFileSize;

    public void Activate()
    {
        if (_isActive)
        {
            return;
        }
        _isActive = true;

        _invalidHandler = AddonRoot.RegisterInvalidHandler(() => IsValid = false);
        if (!IsValid)
        {
            return;
        }
    }

    public void Deactivate()
    {
        if (!_isActive)
        {
            return;
        }
        _isActive = false;

        Utils.DisposeAndSetNull(ref _invalidHandler);
    }

    /// <summary>
    /// Creates one finding command (was the local <c>CreateFindingCommand(Func&lt;int&gt;, string)</c> helper).
    /// </summary>
    private async Task RunFindingAsync(Func<int> find, string findingFuncName)
    {
        int? count = null;
        try
        {
            count = find();
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Exception occurred during the {nameof(FileCleanerViewModel)} finding function: {{FuncName}}", findingFuncName);
            var dialogService = AppServices.TryGetService<IDialogService>();
            if (dialogService is not null)
            {
                await dialogService.ShowExceptionAsync(ex);
            }
        }
        if (count.HasValue)
        {
            var dialogService = AppServices.TryGetService<IDialogService>();
            if (dialogService is not null)
            {
                await dialogService.ShowInfoAsync(Texts.ItemsFoundWithCount.FormatNoThrow(count.Value));
            }
        }
    }

    private bool CanFind() => IsGlobalSearch || _sourceAddonIds.Count > 0;

    [RelayCommand(CanExecute = nameof(CanFind))]
    private Task FindAllCleanableAsync() => RunFindingAsync(FindAllCleanable, nameof(FindAllCleanable));

    [RelayCommand(CanExecute = nameof(CanFind))]
    private Task FindRedundantVpkFilesAsync() => RunFindingAsync(FindRedundantVpkFiles, nameof(FindRedundantVpkFiles));

    [RelayCommand(CanExecute = nameof(CanFind))]
    private Task FindDownloadTempFilesAsync() => RunFindingAsync(FindDownloadTempFiles, nameof(FindDownloadTempFiles));

    [RelayCommand(CanExecute = nameof(CanFind))]
    private Task FindEmptyDirectoriesAsync() => RunFindingAsync(FindEmptyDirectories, nameof(FindEmptyDirectories));

    private bool CanDeleteAll() => _stagedItems.Count > 0;

    [RelayCommand(CanExecute = nameof(CanDeleteAll))]
    private async Task DeleteAllAsync()
    {
        var dialogService = AppServices.TryGetService<IDialogService>();
        if (dialogService is not null)
        {
            bool confirm = await dialogService.ConfirmAsync(Texts.ConfirmDeleteAll);
            if (!confirm)
            {
                return;
            }
        }

        StagedItem[] items = [.. _stagedItems];
        foreach (var item in items)
        {
            item.Delete();
        }
    }

    private bool CanRetainAll() => _stagedItems.Count > 0;

    [RelayCommand(CanExecute = nameof(CanRetainAll))]
    private void RetainAll()
    {
        ClearStagedItems();
    }

    public int FindAllCleanable()
    {
        int count = 0;
        count += FindRedundantVpkFiles();
        count += FindDownloadTempFiles();
        count += FindEmptyDirectories();
        return count;
    }

    public int FindRedundantVpkFiles()
    {
        int count = 0;
        foreach (var addon in ActualSourceAddons.SelectMany(addon => addon.GetSelfAndDescendants()))
        {
            if (addon is WorkshopVpkAddon workshopVpkAddon)
            {
                var report = workshopVpkAddon.RequestDeleteRedundantVpkFiles();
                foreach (var file in report.Files)
                {
                    if (TryAddStagedItem(file))
                    {
                        count++;
                    }
                }
            }
        }
        return count;
    }

    public int FindDownloadTempFiles()
    {
        int count = 0;
        foreach (var addon in ActualSourceAddons.SelectMany(addon => addon.GetSelfAndDescendants()))
        {
            if (addon is WorkshopVpkAddon)
            {
                var dir = addon.FullFilePath;
                if (Directory.Exists(dir))
                {
                    foreach (var file in EnumerateDownloadTempFilesInDirectory(dir))
                    {
                        if (TryAddStagedItem(file))
                        {
                            count++;
                        }
                    }
                }
            }
        }
        return count;
    }

    public int FindEmptyDirectories()
    {
        int count = 0;
        var addonRoot = AddonRoot;

        bool DirectoryMatchExistingAddonNode(string dir)
            => addonRoot.TryGetNodeByPath(addonRoot.ConvertFilePathToNodePath(dir)) is not null;

        if (IsGlobalSearch)
        {
            string rootDir = addonRoot.DirectoryPath;
            foreach (string dir in Directory.EnumerateDirectories(rootDir))
            {
                string dirName = Path.GetFileName(dir);
                if (dirName.StartsWith('.'))
                {
                    continue;
                }

                if (FileSystemUtils.IsDirectoryEmpty(dir))
                {
                    if (!DirectoryMatchExistingAddonNode(dir))
                    {
                        if (TryAddStagedItem(dir))
                        {
                            count++;
                        }
                    }
                    continue;
                }

                foreach (string subDir in FileSystemUtils.FindEmptyDirectories(dir))
                {
                    if (DirectoryMatchExistingAddonNode(subDir))
                    {
                        continue;
                    }
                    if (TryAddStagedItem(subDir))
                    {
                        count++;
                    }
                }
            }
        }
        else
        {
            foreach (var addon in ActualSourceAddons)
            {
                string dir = addon.FullFilePath;
                if (Directory.Exists(dir))
                {
                    foreach (string subDir in FileSystemUtils.FindEmptyDirectories(dir))
                    {
                        if (DirectoryMatchExistingAddonNode(subDir))
                        {
                            continue;
                        }
                        if (TryAddStagedItem(subDir))
                        {
                            count++;
                        }
                    }
                }
            }
        }

        return count;
    }

    private IEnumerable<string> EnumerateDownloadTempFilesInDirectory(string dir)
    {
        foreach (string file in Directory.EnumerateFiles(dir))
        {
            if (file.EndsWith(IDownloadService.DownloadingFileExtension) || file.EndsWith(IDownloadService.DownloadInfoFileExtension))
            {
                yield return file;
            }
        }
    }

    public void ClearStagedItems() => _stagedItems.Clear();

    public bool AddSourceAddon(Guid id)
    {
        if (_sourceAddonIds.Contains(id))
        {
            return false;
        }
        _sourceAddonIds.Add(id);
        return true;
    }

    public bool RemoveSourceAddon(Guid id)
    {
        return _sourceAddonIds.Remove(id);
    }

    private bool TryAddStagedItem(string path)
    {
        path = FileSystemUtils.NormalizePath(path);
        if (ContainsStagedItem(path))
        {
            return false;
        }
        _stagedItems.Add(new StagedItem(this, path));
        return true;
    }

    private bool ContainsStagedItem(string path)
    {
        foreach (var item in _stagedItems)
        {
            if (item.Path == path)
            {
                return true;
            }
        }
        return false;
    }

    private void OnStagedItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateTotalFileSize();

        DeleteAllCommand.NotifyCanExecuteChanged();
        RetainAllCommand.NotifyCanExecuteChanged();
    }

    private void OnSourceAddonIdsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildSourceAddonViewModels();

        FindAllCleanableCommand.NotifyCanExecuteChanged();
        FindRedundantVpkFilesCommand.NotifyCanExecuteChanged();
        FindDownloadTempFilesCommand.NotifyCanExecuteChanged();
        FindEmptyDirectoriesCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Replaces <c>_sourceAddonIds.ToObservableChangeSet().Transform(...).Bind(out ...)</c>.</summary>
    private void RebuildSourceAddonViewModels()
    {
        var viewModels = new ObservableCollection<SourceAddonViewModel>();
        foreach (var id in _sourceAddonIds)
        {
            viewModels.Add(new SourceAddonViewModel(this, id));
        }
        _sourceAddonViewModels = new ReadOnlyObservableCollection<SourceAddonViewModel>(viewModels);
        OnPropertyChanged(nameof(SourceAddonViewModels));
    }

    /// <summary>Replaces <c>_stagedItems.ToObservableChangeSet().Sum(item => item.FileSize ?? 0)</c>.</summary>
    private void UpdateTotalFileSize()
    {
        long total = 0;
        foreach (var item in _stagedItems)
        {
            total += item.FileSize ?? 0;
        }
        if (total != _totalFileSize)
        {
            _totalFileSize = total;
            OnPropertyChanged(nameof(TotalFileSize));
        }
    }
}
