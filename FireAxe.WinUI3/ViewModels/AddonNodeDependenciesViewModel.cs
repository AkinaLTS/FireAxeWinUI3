// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\AddonNodeDependenciesViewModel.cs
// DynamicData Transform+Bind replaced by a plain rebuild on CollectionChanged
// (the project does not take a System.Reactive/DynamicData dependency, see PORTING_PLAN §7 D7).
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace FireAxe.ViewModels;

public class AddonNodeDependenciesViewModel : ViewModelBase, IViewModelLifecycle, IValidity
{
    public class DependencyViewModel : AddonNodeSimpleViewModel
    {
        private readonly AddonNodeDependenciesViewModel _host;

        internal DependencyViewModel(AddonNodeDependenciesViewModel host, Guid dependencyId)
            : base(host.Addon.Root, dependencyId)
        {
            _host = host;
        }

        public void Remove()
        {
            _host.Addon.RemoveDependentAddon(AddonId);
        }
    }

    private static readonly ReadOnlyObservableCollection<DependencyViewModel> EmptyViewModels =
        new(new ObservableCollection<DependencyViewModel>());

    private bool _isValid = true;
    private bool _isActive = false;

    private IDisposable? _invalidHandler = null;

    private ReadOnlyObservableCollection<DependencyViewModel> _dependencyViewModels = EmptyViewModels;

    public AddonNodeDependenciesViewModel(AddonNode addon)
    {
        ArgumentNullException.ThrowIfNull(addon);

        Addon = addon;
    }

    public bool IsValid
    {
        get => _isValid;
        private set => SetProperty(ref _isValid, value);
    }

    public AddonNode Addon { get; }

    public ReadOnlyObservableCollection<DependencyViewModel> DependencyViewModels
    {
        get => _dependencyViewModels;
        private set => SetProperty(ref _dependencyViewModels, value);
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

        ((INotifyCollectionChanged)Addon.DependentAddonIds).CollectionChanged += OnDependentAddonIdsChanged;
        Rebuild();
    }

    public void Deactivate()
    {
        if (!_isActive)
        {
            return;
        }
        _isActive = false;

        ((INotifyCollectionChanged)Addon.DependentAddonIds).CollectionChanged -= OnDependentAddonIdsChanged;
        Utils.DisposeAndSetNull(ref _invalidHandler);

        DependencyViewModels = EmptyViewModels;
    }

    private void OnDependentAddonIdsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Rebuild();
    }

    private void Rebuild()
    {
        var viewModels = new ObservableCollection<DependencyViewModel>();
        foreach (var id in Addon.DependentAddonIds)
        {
            viewModels.Add(new DependencyViewModel(this, id));
        }
        DependencyViewModels = new ReadOnlyObservableCollection<DependencyViewModel>(viewModels);
    }
}
