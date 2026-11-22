// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\AddonProblemListViewModel.cs
// ReactiveUI/DynamicData -> CommunityToolkit.Mvvm + BCL events:
//   * IActivatableViewModel/WhenActivated                 -> IViewModelLifecycle (Activate/Deactivate)
//   * DynamicData ToObservableChangeSet/DistinctValues/Transform/Bind
//                                                          -> CollectionChanged handler that appends one
//                                                             AddonNodeSimpleViewModel per distinct addon
//   * WhenAnyValue(x => x.Count) subscriptions            -> PropertyChanged handlers on "Count"
//   * WhenAnyValue(x => x.Addon) subscription             -> PropertyChanged handler on the VM itself
// NOTE: the Avalonia version forgot to register the disposable that removed the
// `AddonRoot.ProblemProduced` handler (the `+=` was never undone), so a re-activated window ended up
// subscribed twice and outlived the window. Deactivate() now removes it, which the
// Activate/Deactivate re-entrancy rule requires.
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace FireAxe.ViewModels;

public sealed class AddonProblemListViewModel : ViewModelBase, IViewModelLifecycle, IValidity
{
    public class ProblemViewModel : ViewModelBase, IValidity
    {
        private readonly ObservableValidRefCollection<AddonProblem> _problems = new();

        private readonly ObservableCollection<AddonNodeSimpleViewModel> _addonViewModelsSource = new();
        private readonly ReadOnlyObservableCollection<AddonNodeSimpleViewModel> _addonViewModels;

        private bool _isValid = true;

        internal ProblemViewModel(AddonProblem problem)
        {
            ProblemType = problem.GetType();

            _addonViewModels = new ReadOnlyObservableCollection<AddonNodeSimpleViewModel>(_addonViewModelsSource);

            _problems.CollectionChanged += OnProblemsCollectionChanged;
            _problems.PropertyChanged += OnProblemsPropertyChanged;

            _problems.Add(problem);
        }

        public bool IsValid
        {
            get => _isValid;
            private set => SetProperty(ref _isValid, value);
        }

        public Type ProblemType { get; }

        public string Description => AddonProblemTypeExplanations.Get(ProblemType);

        public ReadOnlyObservableCollection<AddonNodeSimpleViewModel> AddonViewModels => _addonViewModels;

        internal void AddProblem(AddonProblem problem)
        {
            if (problem.GetType() != ProblemType)
            {
                return;
            }
            if (_problems.Contains(problem))
            {
                return;
            }
            _problems.Add(problem);
        }

        internal void DisposeSubscriptions()
        {
            _problems.CollectionChanged -= OnProblemsCollectionChanged;
            _problems.PropertyChanged -= OnProblemsPropertyChanged;
            _problems.Dispose();
            IsValid = false;
        }

        private void OnProblemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Was: ToObservableChangeSet().DistinctValues(problem => problem.Addon)
            //      .Transform(addon => new AddonNodeSimpleViewModel(addon)).Bind(out _addonViewModels)
            foreach (var problem in _problems)
            {
                var addon = problem.Addon;
                if (_addonViewModelsSource.Any(viewModel => viewModel.Addon == addon))
                {
                    continue;
                }
                _addonViewModelsSource.Add(new AddonNodeSimpleViewModel(addon));
            }
        }

        private void OnProblemsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Count")
            {
                if (_problems.Count == 0)
                {
                    IsValid = false;
                }
            }
        }
    }

    public class ProblematicAddonViewModel : AddonNodeSimpleViewModel, IValidity
    {
        private AddonNode? _addonProblemsObserved = null;

        private bool _isValid = true;

        public ProblematicAddonViewModel(AddonNode addon) : base(addon)
        {
            _addonProblemsObserved = addon;
            ((INotifyPropertyChanged)addon.Problems).PropertyChanged += OnAddonProblemsPropertyChanged;

            PropertyChanged += OnSelfPropertyChanged;

            this.RegisterInvalidHandler(OnSelfInvalid);
        }

        public bool IsValid
        {
            get => _isValid;
            private set => SetProperty(ref _isValid, value);
        }

        internal void DisposeSubscriptions()
        {
            IsValid = false;
        }

        private void OnAddonProblemsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "Count")
            {
                return;
            }
            if (_addonProblemsObserved is { } addon && addon.Problems.Count == 0)
            {
                IsValid = false;
            }
        }

        private void OnSelfPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Addon) && Addon is null)
            {
                IsValid = false;
            }
        }

        private void OnSelfInvalid()
        {
            if (_addonProblemsObserved is { } addon)
            {
                ((INotifyPropertyChanged)addon.Problems).PropertyChanged -= OnAddonProblemsPropertyChanged;
                _addonProblemsObserved = null;
            }
        }
    }

    private static readonly ReadOnlyObservableCollection<ProblemViewModel> EmptyProblemViewModels =
        new(new ObservableCollection<ProblemViewModel>());

    private static readonly ReadOnlyObservableCollection<ProblematicAddonViewModel> EmptyProblematicAddonViewModels =
        new(new ObservableCollection<ProblematicAddonViewModel>());

    private ObservableValidRefCollection<ProblemViewModel>? _problemViewModels = null;

    private ObservableValidRefCollection<ProblematicAddonViewModel>? _problematicAddonViewModels = null;

    private bool _isActive = false;
    private IDisposable? _invalidHandler = null;

    private bool _isValid = true;

    private ReadOnlyObservableCollection<ProblemViewModel> _problemViewModelCollection = EmptyProblemViewModels;
    private ReadOnlyObservableCollection<ProblematicAddonViewModel> _problematicAddonViewModelCollection = EmptyProblematicAddonViewModels;

    public AddonProblemListViewModel(AddonRoot addonRoot)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);

        AddonRoot = addonRoot;
    }

    public bool IsValid
    {
        get => _isValid;
        private set => SetProperty(ref _isValid, value);
    }

    public AddonRoot AddonRoot { get; }

    public ReadOnlyObservableCollection<ProblemViewModel> ProblemViewModels
    {
        get => _problemViewModelCollection;
        private set => SetProperty(ref _problemViewModelCollection, value);
    }

    public ReadOnlyObservableCollection<ProblematicAddonViewModel> ProblematicAddonViewModels
    {
        get => _problematicAddonViewModelCollection;
        private set => SetProperty(ref _problematicAddonViewModelCollection, value);
    }

    /// <summary>
    /// Called by the View when it is attached (was ReactiveUI's <c>WhenActivated</c>).
    /// </summary>
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

        var problemViewModels = new ObservableValidRefCollection<ProblemViewModel>();
        _problemViewModels = problemViewModels;
        ProblemViewModels = problemViewModels.AsReadOnlyObservableCollection();

        var problematicAddonViewModels = new ObservableValidRefCollection<ProblematicAddonViewModel>();
        _problematicAddonViewModels = problematicAddonViewModels;
        ProblematicAddonViewModels = problematicAddonViewModels.AsReadOnlyObservableCollection();

        AddonRoot.ProblemProduced += NotifyProblemProduced;

        foreach (var addon in AddonRoot.GetDescendants())
        {
            foreach (var problem in addon.Problems)
            {
                NotifyProblemProduced(problem);
            }
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

        AddonRoot.ProblemProduced -= NotifyProblemProduced;
        Utils.DisposeAndSetNull(ref _invalidHandler);

        DisposeProblemViewModels();
        DisposeProblematicAddonViewModels();
    }

    public void Check()
    {
        AddonRoot.Check();
    }

    private void DisposeProblemViewModels()
    {
        if (_problemViewModels is { } problemViewModels)
        {
            problemViewModels.Dispose();
            foreach (var problemViewModel in problemViewModels)
            {
                problemViewModel.DisposeSubscriptions();
            }
        }
        _problemViewModels = null;
        ProblemViewModels = EmptyProblemViewModels;
    }

    private void DisposeProblematicAddonViewModels()
    {
        if (_problematicAddonViewModels is { } problematicAddonViewModels)
        {
            problematicAddonViewModels.Dispose();
            foreach (var problematicAddonViewModel in problematicAddonViewModels)
            {
                problematicAddonViewModel.DisposeSubscriptions();
            }
        }
        _problematicAddonViewModels = null;
        ProblematicAddonViewModels = EmptyProblematicAddonViewModels;
    }

    private void NotifyProblemProduced(AddonProblem problem)
    {
        if (problem is AddonChildrenProblem)
        {
            return;
        }

        if (_problemViewModels is not null)
        {
            var problemType = problem.GetType();
            bool problemTypePresent = false;
            foreach (var problemViewModel in _problemViewModels)
            {
                if (problemViewModel.ProblemType == problemType)
                {
                    problemTypePresent = true;
                    problemViewModel.AddProblem(problem);
                    break;
                }
            }
            if (!problemTypePresent)
            {
                _problemViewModels.Add(new ProblemViewModel(problem));
            }
        }

        if (_problematicAddonViewModels is not null)
        {
            bool addonPresent = false;
            var problematicAddonViewModels = _problematicAddonViewModels.ToArray();
            foreach (var problematicAddonViewModel in problematicAddonViewModels)
            {
                if (problematicAddonViewModel.Addon == problem.Addon)
                {
                    addonPresent = true;
                    break;
                }
            }
            if (!addonPresent)
            {
                _problematicAddonViewModels.Add(new ProblematicAddonViewModel(problem.Addon));
            }
        }
    }
}
