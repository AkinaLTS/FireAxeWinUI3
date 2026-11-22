// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\AddonNodePickerViewModel.cs
// ReactiveUI -> CommunityToolkit.Mvvm + BCL events:
//   * IActivatableViewModel/WhenActivated          -> IViewModelLifecycle (Activate/Deactivate)
//   * ExplorerViewModel.WhenAnyValue(CurrentGroup) -> PropertyChanged subscription (moved from the
//     constructor into the ctor-owned field so Dispose can release it)
//   * ReactiveCommand canExecute (HasSelection)    -> [RelayCommand(CanExecute = ...)] +
//     NotifyCanExecuteChanged from the same PropertyChanged subscription
// The window still closes itself via the CloseRequested event; that is unchanged.
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace FireAxe.ViewModels;

public partial class AddonNodePickerViewModel : ViewModelBase, IViewModelLifecycle, IValidity, IDisposable
{
    private static readonly ValidRef<AddonGroup> s_lastAccessedGroupRef = new();

    private bool _disposed = false;
    private bool _isActive = false;

    private IDisposable? _invalidHandler = null;

    private bool _isValid = true;

    public AddonNodePickerViewModel(AddonRoot addonRoot)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);

        AddonRoot = addonRoot;

        ExplorerViewModel = new(addonRoot);
        ExplorerViewModel.GotoGroup(LastAccessedGroup);
        ExplorerViewModel.PropertyChanged += OnExplorerViewModelPropertyChanged;
    }

    ~AddonNodePickerViewModel()
    {
        Dispose(false);
    }

    public static AddonGroup? LastAccessedGroup
    {
        get => s_lastAccessedGroupRef.TryGet();
        private set => s_lastAccessedGroupRef.Set(value);
    }

    public bool IsValid
    {
        get => _isValid;
        protected set => SetProperty(ref _isValid, value);
    }

    public AddonRoot AddonRoot { get; }

    public AddonNodeExplorerViewModel ExplorerViewModel { get; }

    public bool AllowMultiple { get; init; } = true;

    public event Action? CloseRequested = null;

    public IReadOnlyList<AddonNode> GetSelectedAddons()
    {
        return ExplorerViewModel.SelectedNodes.ToArray();
    }

    /// <summary>
    /// Called by the View when it is attached (was ReactiveUI's <c>WhenActivated</c>).
    /// </summary>
    public void Activate()
    {
        if (_isActive || _disposed)
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

    /// <summary>
    /// Called by the View when it is detached. Idempotent and re-entrant
    /// (Activate -> Deactivate -> Activate is legal).
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

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // The Avalonia version relied on the view's CompositeDisposable to unregister the
                // invalid handler; here Dispose owns it (same as AddonNodeExplorerViewModel).
                Deactivate();
                IsValid = false;
                ExplorerViewModel.PropertyChanged -= OnExplorerViewModelPropertyChanged;
                ExplorerViewModel.Dispose();
            }

            _disposed = true;
        }
    }

    private bool CanConfirm() => ExplorerViewModel.HasSelection;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        CloseRequested?.Invoke();
    }

    private void OnExplorerViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AddonNodeExplorerViewModel.CurrentGroup))
        {
            LastAccessedGroup = ExplorerViewModel.CurrentGroup;
        }
        else if (e.PropertyName == nameof(AddonNodeExplorerViewModel.HasSelection))
        {
            ConfirmCommand.NotifyCanExecuteChanged();
        }
    }
}
