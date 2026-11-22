// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\AddonNameAutoSetterViewModel.cs
// Mechanisms replaced:
//   DynamicData (_targetAddonIds.ToObservableChangeSet().Transform(...).Bind(out ...)) -> mirrored ObservableCollection<TargetAddonViewModel>
//     kept in sync from the id collection's CollectionChanged (incremental, same order/identity)
//   ReactiveCommand.CreateFromTask/Create(..., <observable canExecute>) -> [RelayCommand(CanExecute = ...)]
//     + explicit NotifyCanExecuteChanged() (the CanExecute sources are ObservableCollections, not observable properties)
//   Interaction<Unit, Unit> ShowNoItemsToStartInteraction -> IDialogService.ShowInfoAsync(Texts.NoItemsToStart)
//   Interaction<int, Unit> ShowMultipleFailedApplyInteraction -> IDialogService.ShowErrorAsync(Texts.FailedToApplyNameWithCount.FormatNoThrow(count))
//     (texts/titles taken verbatim from Views\AddonNameAutoSetterWindow.axaml.cs's RegisterHandler blocks)
//   IActivatableViewModel + WhenActivated(this.RegisterInvalidHandler(...).DisposeWith) -> IViewModelLifecycle
//   Command="{Binding ViewModel.RemoveJobViewModel}" (method used as a command in XAML) -> [RelayCommand] RemoveJobViewModelCommand
//   ViewModelActivator Activator -> dropped (ReactiveUI type; same as the other ported ViewModels)
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FireAxe.Resources;
using FireAxe.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

namespace FireAxe.ViewModels;

public sealed partial class AddonNameAutoSetterViewModel : ViewModelBase, IViewModelLifecycle, IValidity, IDisposable
{
    public class TargetAddonViewModel : AddonNodeSimpleViewModel
    {
        private readonly AddonNameAutoSetterViewModel _host;

        public TargetAddonViewModel(AddonNameAutoSetterViewModel host, Guid addonId) : base(host.AddonRoot, addonId)
        {
            _host = host;
        }

        public void Remove()
        {
            _host.RemoveTargetAddon(AddonId);
        }
    }

    private bool _disposed = false;
    private bool _isActive = false;

    private IDisposable? _invalidHandler = null;

    private readonly ObservableCollection<AddonNameAutoSetJobViewModel> _jobViewModels = new();
    private readonly ReadOnlyObservableCollection<AddonNameAutoSetJobViewModel> _jobViewModelsReadOnly;

    private readonly ObservableCollection<Guid> _targetAddonIds = new();
    private readonly ReadOnlyObservableCollection<Guid> _targetAddonIdsReadOnly;
    private readonly ObservableCollection<TargetAddonViewModel> _targetAddonViewModels = new();
    private readonly ReadOnlyObservableCollection<TargetAddonViewModel> _targetAddonViewModelsReadOnly;

    public AddonNameAutoSetterViewModel(AddonRoot addonRoot)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);

        AddonRoot = addonRoot;

        _jobViewModelsReadOnly = new(_jobViewModels);
        _targetAddonIdsReadOnly = new(_targetAddonIds);
        _targetAddonViewModelsReadOnly = new(_targetAddonViewModels);

        _targetAddonIds.CollectionChanged += OnTargetAddonIdsCollectionChanged;

        // Initial values (a partial property cannot carry an initializer).
        IsValid = true;
        ForceUseWorkshopTitle = false;
    }

    [ObservableProperty]
    public partial bool IsValid { get; private set; }

    public AddonRoot AddonRoot { get; }

    public ReadOnlyObservableCollection<AddonNameAutoSetJobViewModel> JobViewModels => _jobViewModelsReadOnly;

    public ReadOnlyObservableCollection<Guid> TargetAddonIds => _targetAddonIdsReadOnly;

    public IEnumerable<AddonNode> TargetAddons
    {
        get
        {
            var addonRoot = AddonRoot;
            foreach (var id in _targetAddonIds)
            {
                if (addonRoot.TryGetNodeById(id, out var addon))
                {
                    yield return addon;
                }
            }
        }
    }

    public ReadOnlyObservableCollection<TargetAddonViewModel> TargetAddonViewModels => _targetAddonViewModelsReadOnly;

    [ObservableProperty]
    public partial bool ForceUseWorkshopTitle { get; set; }

    /// <summary>
    /// Called by the View when it is attached to the visual tree
    /// (was ReactiveUI's <c>WhenActivated</c>, which registered the invalid handler).
    /// </summary>
    public void Activate()
    {
        if (_isActive || _disposed)
        {
            return;
        }
        _isActive = true;

        var addonRoot = AddonRoot;

        _invalidHandler = addonRoot.RegisterInvalidHandler(() => IsValid = false);
        if (!IsValid)
        {
            return;
        }
    }

    /// <summary>
    /// Called by the View when it is detached (was the end of <c>WhenActivated</c>, which disposed the invalid handler).
    /// </summary>
    public void Deactivate()
    {
        if (!_isActive)
        {
            return;
        }
        _isActive = false;

        Utils.DisposeAndSetNull(ref _invalidHandler);
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (!Start())
        {
            await ShowNoItemsToStartAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(HasJobs))]
    private async Task ApplyAllAsync()
    {
        int errorCount = 0;
        foreach (var jobViewModel in _jobViewModels)
        {
            if (jobViewModel.IsApplied)
            {
                continue;
            }
            if (!jobViewModel.Apply())
            {
                errorCount++;
            }
        }
        if (errorCount > 0)
        {
            await ShowMultipleFailedApplyAsync(errorCount);
        }
    }

    [RelayCommand(CanExecute = nameof(HasJobs))]
    private void RetryAll()
    {
        foreach (var jobViewModel in _jobViewModels)
        {
            jobViewModel.Retry();
        }
    }

    [RelayCommand(CanExecute = nameof(HasJobs))]
    private void RemoveAll()
    {
        foreach (var jobViewModel in _jobViewModels)
        {
            jobViewModel.Dispose();
        }
        _jobViewModels.Clear();

        NotifyJobsCanExecuteChanged();
    }

    private bool CanStart()
    {
        return _targetAddonIds.Count > 0;
    }

    private bool HasJobs()
    {
        return _jobViewModels.Count > 0;
    }

    public bool Start()
    {
        this.ThrowIfInvalid();

        bool hasItemsToStart = false;
        foreach (var addon in TargetAddons.SelectMany(addon => addon.GetSelfAndDescendants()))
        {
            if (TryAddJob(addon))
            {
                hasItemsToStart = true;
            }
        }
        return hasItemsToStart;
    }

    public bool AddTargetAddon(Guid id)
    {
        this.ThrowIfInvalid();

        if (_targetAddonIds.Contains(id))
        {
            return false;
        }
        _targetAddonIds.Add(id);
        return true;
    }

    public bool RemoveTargetAddon(Guid id)
    {
        this.ThrowIfInvalid();

        return _targetAddonIds.Remove(id);
    }

    [RelayCommand]
    public void RemoveJobViewModel(AddonNameAutoSetJobViewModel jobViewModel)
    {
        _jobViewModels.Remove(jobViewModel);

        NotifyJobsCanExecuteChanged();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            IsValid = false;
            foreach (var jobViewModel in _jobViewModels)
            {
                jobViewModel.Dispose();
            }

            _disposed = true;
        }
    }

    private bool TryAddJob(AddonNode addon)
    {
        if (!addon.CanGetSuggestedName)
        {
            return false;
        }
        foreach (var jobViewModel in _jobViewModels)
        {
            if (jobViewModel.Addon == addon)
            {
                return false;
            }
        }

        object? settingArg = null;
        if (ForceUseWorkshopTitle && addon is WorkshopVpkAddon)
        {
            settingArg = WorkshopVpkAddon.ForceUseWorkshopTitle;
        }
        _jobViewModels.Add(new AddonNameAutoSetJobViewModel(addon, settingArg));

        NotifyJobsCanExecuteChanged();
        return true;
    }

    /// <summary>
    /// Replaces the observable <c>CanExecute</c> pipelines of <c>ApplyAllCommand</c> / <c>RetryAllCommand</c> /
    /// <c>RemoveAllCommand</c>, which depended on the job collection's count.
    /// </summary>
    private void NotifyJobsCanExecuteChanged()
    {
        ApplyAllCommand.NotifyCanExecuteChanged();
        RetryAllCommand.NotifyCanExecuteChanged();
        RemoveAllCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Mirrors the target addon id list into <see cref="TargetAddonViewModels"/>
    /// (replaces DynamicData's <c>Transform</c> + <c>Bind</c>) and refreshes <c>StartCommand</c>'s CanExecute
    /// (which depended on the id collection's count).
    /// </summary>
    private void OnTargetAddonIdsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                {
                    if (e.NewItems is { } newItems && e.NewStartingIndex >= 0)
                    {
                        for (int i = 0; i < newItems.Count; i++)
                        {
                            if (newItems[i] is Guid id)
                            {
                                _targetAddonViewModels.Insert(e.NewStartingIndex + i, new TargetAddonViewModel(this, id));
                            }
                        }
                    }
                    break;
                }
            case NotifyCollectionChangedAction.Remove:
                {
                    if (e.OldItems is { } oldItems && e.OldStartingIndex >= 0)
                    {
                        for (int i = 0; i < oldItems.Count; i++)
                        {
                            if (e.OldStartingIndex < _targetAddonViewModels.Count)
                            {
                                _targetAddonViewModels.RemoveAt(e.OldStartingIndex);
                            }
                        }
                    }
                    break;
                }
            case NotifyCollectionChangedAction.Replace:
                {
                    if (e.OldItems is { } oldItems && e.NewItems is { } newItems && e.OldStartingIndex >= 0)
                    {
                        for (int i = 0; i < oldItems.Count && i < newItems.Count; i++)
                        {
                            int index = e.OldStartingIndex + i;
                            if (index < _targetAddonViewModels.Count && newItems[i] is Guid id)
                            {
                                _targetAddonViewModels[index] = new TargetAddonViewModel(this, id);
                            }
                        }
                    }
                    break;
                }
            case NotifyCollectionChangedAction.Move:
                {
                    if (e.OldStartingIndex >= 0 && e.NewStartingIndex >= 0
                        && e.OldStartingIndex < _targetAddonViewModels.Count
                        && e.NewStartingIndex < _targetAddonViewModels.Count)
                    {
                        _targetAddonViewModels.Move(e.OldStartingIndex, e.NewStartingIndex);
                    }
                    break;
                }
            case NotifyCollectionChangedAction.Reset:
                {
                    _targetAddonViewModels.Clear();
                    break;
                }
        }

        StartCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Replaces <c>ShowNoItemsToStartInteraction</c>
    /// (was <c>CommonMessageBoxes.ShowInfo(this, Texts.NoItemsToStart)</c>).
    /// </summary>
    private static async Task ShowNoItemsToStartAsync()
    {
        var dialogService = AppServices.TryGetService<IDialogService>();
        if (dialogService is null)
        {
            return;
        }
        await dialogService.ShowInfoAsync(Texts.NoItemsToStart);
    }

    /// <summary>
    /// Replaces <c>ShowMultipleFailedApplyInteraction</c>
    /// (was <c>CommonMessageBoxes.ShowError(this, Texts.FailedToApplyNameWithCount.FormatNoThrow(count))</c>).
    /// </summary>
    private static async Task ShowMultipleFailedApplyAsync(int count)
    {
        var dialogService = AppServices.TryGetService<IDialogService>();
        if (dialogService is null)
        {
            return;
        }
        await dialogService.ShowErrorAsync(Texts.FailedToApplyNameWithCount.FormatNoThrow(count));
    }
}
