// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\VpkAddonConflictingDetailsViewModel.cs
// DynamicData Transform+Bind replaced by rebuild-on-CollectionChanged.
using CommunityToolkit.Mvvm.Input;
using FireAxe.Resources;
using FireAxe.Services;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

namespace FireAxe.ViewModels;

public partial class VpkAddonConflictingDetailsViewModel : ViewModelBase, IViewModelLifecycle, IValidity
{
    private static readonly ReadOnlyObservableCollection<ConflictingVpkAddonWithFilesViewModel> EmptyAddonWithFiles =
        new(new ObservableCollection<ConflictingVpkAddonWithFilesViewModel>());

    private static readonly ReadOnlyObservableCollection<ConflictingVpkFileWithAddonsViewModel> EmptyFileWithAddons =
        new(new ObservableCollection<ConflictingVpkFileWithAddonsViewModel>());

    private bool _isValid = true;
    private bool _isActive = false;

    private IDisposable? _invalidHandler = null;

    private ReadOnlyObservableCollection<ConflictingVpkAddonWithFilesViewModel> _conflictingAddonWithFilesViewModels = EmptyAddonWithFiles;
    private ReadOnlyObservableCollection<ConflictingVpkFileWithAddonsViewModel> _conflictingFileWithAddonsViewModels = EmptyFileWithAddons;

    public VpkAddonConflictingDetailsViewModel(VpkAddon vpkAddon)
    {
        ArgumentNullException.ThrowIfNull(vpkAddon);
        Addon = vpkAddon;
    }

    public bool IsValid
    {
        get => _isValid;
        private set => SetProperty(ref _isValid, value);
    }

    public VpkAddon Addon { get; }

    public ReadOnlyObservableCollection<ConflictingVpkAddonWithFilesViewModel> ConflictingAddonWithFilesViewModels
    {
        get => _conflictingAddonWithFilesViewModels;
        private set => SetProperty(ref _conflictingAddonWithFilesViewModels, value);
    }

    public ReadOnlyObservableCollection<ConflictingVpkFileWithAddonsViewModel> ConflictingFileWithAddonsViewModels
    {
        get => _conflictingFileWithAddonsViewModels;
        private set => SetProperty(ref _conflictingFileWithAddonsViewModels, value);
    }

    public void Activate()
    {
        if (_isActive)
        {
            return;
        }
        _isActive = true;

        _invalidHandler = Addon.RegisterInvalidHandler(() => IsValid = false);
        if (!IsValid)
        {
            return;
        }

        ((INotifyCollectionChanged)Addon.ConflictingAddonIdsWithFiles).CollectionChanged += OnConflictingAddonIdsWithFilesChanged;
        ((INotifyCollectionChanged)Addon.ConflictingFilesWithAddonIds).CollectionChanged += OnConflictingFilesWithAddonIdsChanged;

        RebuildConflictingAddonWithFiles();
        RebuildConflictingFileWithAddons();
    }

    public void Deactivate()
    {
        if (!_isActive)
        {
            return;
        }
        _isActive = false;

        ((INotifyCollectionChanged)Addon.ConflictingAddonIdsWithFiles).CollectionChanged -= OnConflictingAddonIdsWithFilesChanged;
        ((INotifyCollectionChanged)Addon.ConflictingFilesWithAddonIds).CollectionChanged -= OnConflictingFilesWithAddonIdsChanged;
        Utils.DisposeAndSetNull(ref _invalidHandler);

        ConflictingAddonWithFilesViewModels = EmptyAddonWithFiles;
        ConflictingFileWithAddonsViewModels = EmptyFileWithAddons;
    }

    [RelayCommand]
    private async Task IgnoreAllConflictingFilesAsync()
    {
        var dialogService = AppServices.TryGetService<IDialogService>();
        if (dialogService is not null)
        {
            bool confirm = await dialogService.ConfirmAsync(Texts.ConfirmIgnoreAllConflictingFiles, Texts.Warning);
            if (!confirm)
            {
                return;
            }
        }

        IgnoreAllConflictingFiles();
    }

    [RelayCommand]
    private async Task RemoveAllConflictIgnoringFilesAsync()
    {
        var dialogService = AppServices.TryGetService<IDialogService>();
        if (dialogService is not null)
        {
            bool confirm = await dialogService.ConfirmAsync(Texts.ConfirmRemoveAllConflictIgnoringFiles, Texts.Warning);
            if (!confirm)
            {
                return;
            }
        }

        Addon.ClearConflictIgnoringFiles();
    }

    public void IgnoreConflictingFile(string? file)
    {
        if (file is null)
        {
            return;
        }

        Addon.AddConflictIgnoringFile(file);
    }

    public void RemoveConflictIgnoringFile(string? file)
    {
        if (file is null)
        {
            return;
        }

        Addon.RemoveConflictIgnoringFile(file);
    }

    public void IgnoreAllConflictingFiles()
    {
        string[] files = [.. Addon.ConflictingFilesWithAddonIds.Select(item => item.File)];
        foreach (var file in files)
        {
            Addon.AddConflictIgnoringFile(file);
        }
    }

    private void OnConflictingAddonIdsWithFilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildConflictingAddonWithFiles();
    }

    private void OnConflictingFilesWithAddonIdsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildConflictingFileWithAddons();
    }

    private void RebuildConflictingAddonWithFiles()
    {
        var addonRoot = Addon.Root;
        var viewModels = new ObservableCollection<ConflictingVpkAddonWithFilesViewModel>(
            Addon.ConflictingAddonIdsWithFiles.Select(item => new ConflictingVpkAddonWithFilesViewModel(addonRoot, item.AddonId, item.Files)));
        ConflictingAddonWithFilesViewModels = new ReadOnlyObservableCollection<ConflictingVpkAddonWithFilesViewModel>(viewModels);
    }

    private void RebuildConflictingFileWithAddons()
    {
        var addonRoot = Addon.Root;
        var viewModels = new ObservableCollection<ConflictingVpkFileWithAddonsViewModel>(
            Addon.ConflictingFilesWithAddonIds.Select(item => new ConflictingVpkFileWithAddonsViewModel(item.File, addonRoot, item.AddonIds)));
        ConflictingFileWithAddonsViewModels = new ReadOnlyObservableCollection<ConflictingVpkFileWithAddonsViewModel>(viewModels);
    }
}
