// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\VpkAddonConflictListViewModel.cs
// ReactiveUI (ViewModelBase/ReactiveCommand/IActivatableViewModel/WhenActivated/ViewModelActivator) ->
//   FireAxe.ViewModels.ViewModelBase + [ObservableProperty] / [RelayCommand] + IViewModelLifecycle.
// Interaction<Exception, Unit> ShowExceptionInteraction -> IDialogService.ShowExceptionAsync.
// ObservableValidRefCollection / ReadOnlyObservableCollection are kept verbatim (both live in FireAxe.Core).
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FireAxe.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FireAxe.ViewModels;

public sealed partial class VpkAddonConflictListViewModel : ViewModelBase, IViewModelLifecycle, IValidity, IDisposable
{
    private bool _disposed = false;
    private bool _active = false;
    private bool _isActive = false;

    private IDisposable? _invalidHandler = null;

    private ObservableValidRefCollection<VpkAddonConflictingDetailsViewModel> _vpkConflictingDetailsViewModels = new();
    private ReadOnlyObservableCollection<VpkAddonConflictingDetailsViewModel> _vpkConflictingDetailsViewModelsReadOnly;

    private readonly CancellationTokenSource _cts = new();

    public VpkAddonConflictListViewModel(AddonRoot addonRoot)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);
        AddonRoot = addonRoot;

        IsValid = true;
        IsRefreshing = false;
        ConflictingFileWithAddonsViewModels = [];

        _vpkConflictingDetailsViewModelsReadOnly = _vpkConflictingDetailsViewModels.AsReadOnlyObservableCollection();
    }

    [ObservableProperty]
    public partial bool IsValid { get; private set; }

    public AddonRoot AddonRoot { get; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; private set; }

    public ReadOnlyObservableCollection<VpkAddonConflictingDetailsViewModel> VpkConflictingDetailsViewModels => _vpkConflictingDetailsViewModelsReadOnly;

    [ObservableProperty]
    public partial IEnumerable<ConflictingVpkFileWithAddonsViewModel> ConflictingFileWithAddonsViewModels { get; private set; }

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

        _active = true;

        RefreshCommand.Execute(null);
    }

    public void Deactivate()
    {
        if (!_isActive)
        {
            return;
        }
        _isActive = false;

        _active = false;
        Utils.DisposeAndSetNull(ref _invalidHandler);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            IsValid = false;

            _vpkConflictingDetailsViewModels.Dispose();

            _cts.Cancel();
            _cts.Dispose();

            _disposed = true;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        this.ThrowIfInvalid();

        if (IsRefreshing)
        {
            return;
        }

        IsRefreshing = true;
        try
        {
            _vpkConflictingDetailsViewModels.Clear();
            ConflictingFileWithAddonsViewModels = [];

            var cancellationToken = _cts.Token;
            var addonRoot = AddonRoot;

            VpkAddonConflictResult conflictResult;
            try
            {
                conflictResult = await addonRoot.CheckVpkAddonConflictsAsync().WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                if (_active)
                {
                    var dialogService = AppServices.TryGetService<IDialogService>();
                    if (dialogService is not null)
                    {
                        await dialogService.ShowExceptionAsync(ex);
                    }
                }
                return;
            }

            if (!IsValid)
            {
                return;
            }

            _vpkConflictingDetailsViewModels.Reset(
                conflictResult.ConflictingAddons.Select(addon => new VpkAddonConflictingDetailsViewModel(addon)));
            ConflictingFileWithAddonsViewModels = conflictResult.ConflictingFiles
                .Select(file => new ConflictingVpkFileWithAddonsViewModel(file, addonRoot, conflictResult.GetConflictingAddons(file).Select(addon => addon.Id)))
                .ToArray();
        }
        finally
        {
            IsRefreshing = false;
        }
    }
}
