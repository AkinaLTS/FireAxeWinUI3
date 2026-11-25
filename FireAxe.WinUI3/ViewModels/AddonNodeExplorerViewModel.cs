// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\AddonNodeExplorerViewModel.cs
// ReactiveUI/DynamicData -> CommunityToolkit.Mvvm + BCL events:
//   * IActivatableViewModel/WhenActivated                 -> IViewModelLifecycle
//   * ObservableAsPropertyHelper pipelines                -> recompute on CollectionChanged/PropertyChanged
//   * DynamicData ToObservableChangeSet/Sort/Bind/Transform -> rebuild a ReadOnlyObservableCollection
//   * Interaction<T,U> (5)                                -> IDialogService / IWindowService
//   * Avalonia Dispatcher / RxApp.MainThreadScheduler     -> IUiDispatcher
//   * ReactiveCommand (18)                                 -> RelayCommand/AsyncRelayCommand fields, so the
//     public methods the Views call (NewGroup(), GotoParent(), ...) keep their exact names.
using CommunityToolkit.Mvvm.Input;
using FireAxe.Collections;
using FireAxe.Resources;
using FireAxe.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FireAxe.ViewModels;

public class AddonNodeExplorerViewModel : ViewModelBase, IViewModelLifecycle, IValidity, IDisposable
{
    public class MovingNodeViewModel : AddonNodeSimpleViewModel
    {
        private readonly AddonNodeExplorerViewModel _explorerViewModel;

        internal MovingNodeViewModel(AddonNodeExplorerViewModel explorerViewModel, AddonNode addon) : base(addon)
        {
            _explorerViewModel = explorerViewModel;

            MoveHereCommand = new AsyncRelayCommand(async () =>
            {
                try
                {
                    MoveHere();
                }
                catch (Exception ex)
                {
                    var nodePath = Addon?.NodePath ?? "null";
                    Log.Error(ex, "Exception occurred during moving the AddonNode: {NodePath}", nodePath);
                    await _explorerViewModel.ShowMoveExceptionAsync(ex, nodePath);
                }
            });
        }

        public IAsyncRelayCommand MoveHereCommand { get; }

        public void Remove()
        {
            if (Addon is { } addon)
            {
                _explorerViewModel._movingNodes.Remove(addon);
            }
        }

        public void MoveHere()
        {
            if (Addon is { } addon)
            {
                addon.MoveTo(_explorerViewModel.CurrentGroup);
            }
            Remove();
        }
    }

    private static readonly TimeSpan SearchThrottleTime = TimeSpan.FromSeconds(0.5);

    /// <summary>
    /// Fallback tile size, used for kinds that are not tiles at all. Kept identical to the Avalonia
    /// version's <c>GetValueOrDefault(200)</c> so that switching between grid and tile view keeps a
    /// sane number in <see cref="TileViewSize"/>.
    /// </summary>
    private const double DefaultTileViewSize = 200;

    // The Avalonia version looked these up from the theme resources size_addon_tile,
    // size_addon_tile_large and size_addon_tile_small. WinUI 3 has no runtime resource lookup for
    // these, and the values are also mirrored in Views\Theme\Styles.xaml, so they are pinned here.
    private const double MediumTileViewSize = 100;
    private const double LargeTileViewSize = 150;
    private const double SmallTileViewSize = 75;

    private static readonly ReadOnlyObservableCollection<AddonNode> EmptyNodes = new(new ObservableCollection<AddonNode>());
    private static readonly ReadOnlyObservableCollection<MovingNodeViewModel> EmptyMovingNodes = new(new ObservableCollection<MovingNodeViewModel>());

    private bool _disposed = false;
    private bool _isActive = false;

    private readonly AddonRoot _addonRoot;
    private readonly IUiDispatcher? _uiDispatcher;

    private readonly DebounceAction _tagFilterDebounce;
    private readonly DebounceAction _searchOptionsDebounce;
    private readonly DebounceAction _searchTextDebounce;

    private ReadOnlyObservableCollection<AddonNode> _currentNodes = EmptyNodes;
    private SortedObservableCollection<AddonNode>? _sortedNodes;

    private AddonGroup? _currentGroup = null;
    private AddonGroup? _currentGroupObserved = null;

    private readonly ObservableValidRefCollection<AddonNode> _selectedNodes = new();
    private AddonNode? _selectedNode = null;
    private bool _hasSelection = false;
    private bool _isSingleSelection = false;
    private bool _isMultipleSelection = false;
    private string? _selectionNames = null;
    private bool _hasSelectedUpdateableAddon = false;

    private readonly ObservableValidRefCollection<AddonNode> _movingNodes = new();
    private ReadOnlyObservableCollection<MovingNodeViewModel> _movingNodeViewModels = EmptyMovingNodes;

    private AddonNodeSortMethod _sortMethod = AddonNodeSortMethod.None;
    private bool _isAscendingOrder = true;

    private AddonNodeListItemViewKind _listItemViewKind = AddonNodeListItemViewKind.MediumTile;
    private bool _isGridView = false;
    private bool _isTileView = false;
    private double _tileViewSize = DefaultTileViewSize;

    private string _searchText = "";
    private bool _isSearchTextClearable = false;
    private CancellationTokenSource? _searchCts = null;
    private object? _currentSearchId = null;
    private bool _isSearching = false;

    private bool _searchIgnoreCase = true;
    private bool _isSearchFlatten = false;
    private bool _isSearchRegex = false;
    private bool _isFilterEnabled = false;
    private IEnumerable<string>? _existingTags = null;
    private AddonTagFilterMode _tagFilterMode = AddonTagFilterMode.Or;
    private IEnumerable<string> _selectedTags = [];
    private ISet<string>? _filterTags = null;
    private AddonNodeSearchOptions _searchOptions = new();

    private IDisposable? _invalidHandler = null;

    public AddonNodeExplorerViewModel(AddonRoot addonRoot)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);

        _addonRoot = addonRoot;
        _uiDispatcher = AppServices.TryGetService<IUiDispatcher>();

        _tagFilterDebounce = new DebounceAction(SearchThrottleTime, _uiDispatcher);
        _searchOptionsDebounce = new DebounceAction(SearchThrottleTime, _uiDispatcher);
        _searchTextDebounce = new DebounceAction(SearchThrottleTime, _uiDispatcher);

        _selectedNodes.CollectionChanged += OnSelectedNodesChanged;
        _selectedNodes.PropertyChanged += OnSelectedNodesPropertyChanged;
        _movingNodes.CollectionChanged += OnMovingNodesChanged;

        GotoParentCommand = new RelayCommand(GotoParent, CanGotoParent);
        GotoRootCommand = new RelayCommand(() => GotoGroup(null));
        EnableCommand = new RelayCommand(() => SetSelectionEnabled(true), CanOperateOnSelection);
        DisableCommand = new RelayCommand(() => SetSelectionEnabled(false), CanOperateOnSelection);
        EnableRecursivelyCommand = new RelayCommand(() => SetSelectionEnabledRecursively(true), CanOperateOnSelection);
        DisableRecursivelyCommand = new RelayCommand(() => SetSelectionEnabledRecursively(false), CanOperateOnSelection);
        MoveCommand = new RelayCommand(MoveSelection, CanOperateOnSelection);
        MoveHereCommand = new AsyncRelayCommand(MoveSelectionHereAsync, CanMoveHere);
        CancelMoveCommand = new RelayCommand(ClearMovingNodes, CanMoveHere);
        NewGroupCommand = new RelayCommand(() => { NewGroup(); });
        NewRefAddonCommand = new RelayCommand(() => { NewRefAddon(); });
        NewWorkshopAddonCommand = new RelayCommand(() => { NewWorkshopAddon(); });
        NewWorkshopCollectionCommand = new AsyncRelayCommand(NewWorkshopCollectionAsync);
        CreateRefAddonsBasedOnSelectedCommand = new RelayCommand(() => { CreateRefAddonsBasedOnSelected(); }, CanOperateOnSelection);
        DeleteCommand = new AsyncRelayCommand<bool>(DeleteSelectionAsync, _ => CanOperateOnSelection());
        SetAutoUpdateStrategyToDefaultRecursivelyCommand = new RelayCommand(() => SetSelectionAutoUpdateStrategyRecursively(null), CanOperateOnUpdateableSelection);
        SetAutoUpdateStrategyToEnabledRecursivelyCommand = new RelayCommand(() => SetSelectionAutoUpdateStrategyRecursively(true), CanOperateOnUpdateableSelection);
        SetAutoUpdateStrategyToDisabledRecursivelyCommand = new RelayCommand(() => SetSelectionAutoUpdateStrategyRecursively(false), CanOperateOnUpdateableSelection);
        OpenWorkshopPageCommand = new RelayCommand(OpenSelectionWorkshopPage, CanOperateOnUpdateableSelection);
        RefreshCommand = new RelayCommand(Refresh);

        UpdateSelectionDerivedValues();
        UpdateFilterTags();
        UpdateSearchOptions();
        UpdateTileViewMetrics();
    }

    public IRelayCommand GotoParentCommand { get; }

    public IRelayCommand GotoRootCommand { get; }

    public IRelayCommand EnableCommand { get; }

    public IRelayCommand DisableCommand { get; }

    public IRelayCommand EnableRecursivelyCommand { get; }

    public IRelayCommand DisableRecursivelyCommand { get; }

    public IRelayCommand MoveCommand { get; }

    public IAsyncRelayCommand MoveHereCommand { get; }

    public IRelayCommand CancelMoveCommand { get; }

    public IRelayCommand NewGroupCommand { get; }

    public IRelayCommand NewRefAddonCommand { get; }

    public IRelayCommand NewWorkshopAddonCommand { get; }

    public IAsyncRelayCommand NewWorkshopCollectionCommand { get; }

    public IRelayCommand CreateRefAddonsBasedOnSelectedCommand { get; }

    public IAsyncRelayCommand<bool> DeleteCommand { get; }

    public IRelayCommand SetAutoUpdateStrategyToDefaultRecursivelyCommand { get; }

    public IRelayCommand SetAutoUpdateStrategyToEnabledRecursivelyCommand { get; }

    public IRelayCommand SetAutoUpdateStrategyToDisabledRecursivelyCommand { get; }

    public IRelayCommand OpenWorkshopPageCommand { get; }

    public IRelayCommand RefreshCommand { get; }

    public bool IsValid
    {
        get;
        protected set
        {
            if (SetProperty(ref field, value))
            {
                NotifyCommandCanExecuteChanged();
            }
        }
    } = true;

    public AddonRoot AddonRoot => _addonRoot;

    public AddonGroup? CurrentGroup
    {
        get => _currentGroup;
        private set
        {
            if (!SetProperty(ref _currentGroup, value))
            {
                return;
            }

            ObserveCurrentGroup(value);
            NavBarItemViewModels = BuildNavBarItemViewModels(value);

            // Switching groups clears the selection (unchanged from the Avalonia VM).
            _selectedNodes.Clear();

            NotifyCommandCanExecuteChanged();

            // The Avalonia VM refreshed the visible nodes from a combined WhenAnyValue over
            // CurrentGroup / SortMethod / IsAscendingOrder / SearchOptions. The other three raise
            // their own refresh, so CurrentGroup has to do it here; without this the list keeps
            // showing the previous group after GotoGroup/GotoParent/GotoNode.
            RefreshCurrentNodes();
        }
    }

    public ReadOnlyObservableCollection<AddonNode> CurrentNodes
    {
        get => _currentNodes;
        private set => SetProperty(ref _currentNodes, value);
    }

    public AddonNodeSortMethod SortMethod
    {
        get => _sortMethod;
        set
        {
            if (SetProperty(ref _sortMethod, value))
            {
                RefreshCurrentNodes();
            }
        }
    }

    public bool IsAscendingOrder
    {
        get => _isAscendingOrder;
        set
        {
            if (SetProperty(ref _isAscendingOrder, value))
            {
                RefreshCurrentNodes();
            }
        }
    }

    public ReadOnlyObservableCollection<MovingNodeViewModel> MovingNodeViewModels => _movingNodeViewModels;

    public ObservableValidRefCollection<AddonNode> SelectedNodes => _selectedNodes;

    public AddonNode? SelectedNode => _selectedNode;

    public bool HasSelection => _hasSelection;

    public bool IsSingleSelection => _isSingleSelection;

    public bool IsMultipleSelection => _isMultipleSelection;

    public string? SelectionNames => _selectionNames;

    public AddonNodeListItemViewKind ListItemViewKind
    {
        get => _listItemViewKind;
        set
        {
            if (SetProperty(ref _listItemViewKind, value))
            {
                UpdateTileViewMetrics();
            }
        }
    }

    public bool IsGridView => _isGridView;

    public bool IsTileView => _isTileView;

    public double TileViewSize => _tileViewSize;

    public IEnumerable<AddonNodeNavBarItemViewModel> NavBarItemViewModels
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    } = [];

    public IEnumerable<string>? ExistingTags
    {
        get => _existingTags;
        private set => SetProperty(ref _existingTags, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value))
            {
                return;
            }

            IsSearchTextClearable = value.Length > 0;
            _searchTextDebounce.Run(() =>
            {
                if (IsValid)
                {
                    RefreshCurrentNodes();
                }
            });
        }
    }

    public bool IsSearchTextClearable
    {
        get => _isSearchTextClearable;
        private set => SetProperty(ref _isSearchTextClearable, value);
    }

    public bool IsSearching
    {
        get => _isSearching;
        private set => SetProperty(ref _isSearching, value);
    }

    public bool SearchIgnoreCase
    {
        get => _searchIgnoreCase;
        set
        {
            if (SetProperty(ref _searchIgnoreCase, value))
            {
                ScheduleSearchOptionsUpdate();
            }
        }
    }

    public bool IsSearchFlatten
    {
        get => _isSearchFlatten;
        set
        {
            if (SetProperty(ref _isSearchFlatten, value))
            {
                ScheduleSearchOptionsUpdate();
            }
        }
    }

    public bool IsSearchRegex
    {
        get => _isSearchRegex;
        set
        {
            if (SetProperty(ref _isSearchRegex, value))
            {
                ScheduleSearchOptionsUpdate();
            }
        }
    }

    public bool IsFilterEnabled
    {
        get => _isFilterEnabled;
        set
        {
            if (SetProperty(ref _isFilterEnabled, value))
            {
                ScheduleFilterTagsUpdate();
            }
        }
    }

    public AddonTagFilterMode TagFilterMode
    {
        get => _tagFilterMode;
        set
        {
            if (SetProperty(ref _tagFilterMode, value))
            {
                ScheduleSearchOptionsUpdate();
            }
        }
    }

    public IEnumerable<string> SelectedTags
    {
        get => _selectedTags;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _selectedTags = value;
            OnPropertyChanged();
            ScheduleFilterTagsUpdate();
        }
    }

    public ISet<string>? FilterTags
    {
        get => _filterTags;
        private set
        {
            _filterTags = value;
            OnPropertyChanged();
            ScheduleSearchOptionsUpdate();
        }
    }

    public AddonNodeSearchOptions SearchOptions
    {
        get => _searchOptions;
        private set
        {
            _searchOptions = value;
            OnPropertyChanged();
            RefreshCurrentNodes();
        }
    }

    public void SelectNode(AddonNode node)
    {
        this.ThrowIfInvalid();
        ArgumentNullException.ThrowIfNull(node);

        _selectedNodes.Clear();
        if (node.Root == AddonRoot)
        {
            _selectedNodes.Add(node);
        }
    }

    public void SelectNodes(IEnumerable<AddonNode> nodes)
    {
        this.ThrowIfInvalid();
        ArgumentNullException.ThrowIfNull(nodes);

        _selectedNodes.Reset(nodes.Where(node => node.Root == AddonRoot));
    }

    public void GotoGroup(AddonGroup? group)
    {
        this.ThrowIfInvalid();
        if (group is not null)
        {
            if (!group.IsValid || group.Root != AddonRoot)
            {
                return;
            }
        }

        CurrentGroup = group;
    }

    public void GotoParent()
    {
        this.ThrowIfInvalid();

        if (CurrentGroup is { } currentGroup)
        {
            GotoGroup(currentGroup.Group);
        }
    }

    public void GotoNode(AddonNode node)
    {
        this.ThrowIfInvalid();
        ArgumentNullException.ThrowIfNull(node);
        if (!node.IsValid || node.Root != AddonRoot)
        {
            return;
        }

        CurrentGroup = node.Group;
        SelectNode(node);
    }

    public void AddMovingNode(AddonNode node)
    {
        this.ThrowIfInvalid();
        ArgumentNullException.ThrowIfNull(node);
        if (!node.IsValid || node.Root != AddonRoot)
        {
            return;
        }

        if (_movingNodes.Contains(node))
        {
            return;
        }

        _movingNodes.Add(node);
    }

    public void ClearMovingNodes()
    {
        this.ThrowIfInvalid();

        _movingNodes.Clear();
    }

    public void Refresh()
    {
        this.ThrowIfInvalid();

        RefreshCurrentNodes();
    }

    public AddonGroup NewGroup()
    {
        this.ThrowIfInvalid();

        var group = AddonNode.Create<AddonGroup>(AddonRoot, CurrentGroup);
        group.Name = group.Parent.GetUniqueChildName(Texts.UnnamedGroup);
        Directory.CreateDirectory(group.FullFilePath);
        SelectNode(group);
        return group;
    }

    public RefAddonNode NewRefAddon()
    {
        this.ThrowIfInvalid();

        var addon = AddonNode.Create<RefAddonNode>(AddonRoot, CurrentGroup);
        addon.Name = addon.Parent.GetUniqueChildName(Texts.UnnamedReferenceAddon);
        SelectNode(addon);
        return addon;
    }

    public WorkshopVpkAddon NewWorkshopAddon()
    {
        this.ThrowIfInvalid();

        var addon = AddonNode.Create<WorkshopVpkAddon>(AddonRoot, CurrentGroup);
        addon.Name = addon.Parent.GetUniqueChildName(Texts.UnnamedWorkshopAddon);
        addon.RequestAutoSetName = true;
        SelectNode(addon);
        return addon;
    }

    public IReadOnlyList<AddonNode> CreateRefAddonsBasedOnSelected()
    {
        this.ThrowIfInvalid();

        var selected = SelectedNodes.ToArray();
        using var blockAutoCheck = AddonRoot.BlockAutoCheck();
        var addons = RefAddonNode.CreateBasedOn(selected, CurrentGroup);
        foreach (var addon in addons)
        {
            addon.CheckSelfAndDescendants();
        }
        SelectNodes(addons);
        return addons;
    }

    public void ToggleOrderDirection()
    {
        this.ThrowIfInvalid();

        IsAscendingOrder = !IsAscendingOrder;
    }

    public void ClearSearchText()
    {
        this.ThrowIfInvalid();

        SearchText = "";
    }

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

        AddonRoot.DescendantNodeMoved += OnAddonRootDescendantNodeMoved;
        AddonRoot.NewNodeIdRegistered += OnAddonRootNewNodeIdRegistered;

        UpdateExistingTags();
        ((INotifyCollectionChanged)AddonRoot.CustomTags).CollectionChanged += OnCustomTagsChanged;

        Refresh();
    }

    public void Deactivate()
    {
        if (!_isActive)
        {
            return;
        }
        _isActive = false;

        AddonRoot.DescendantNodeMoved -= OnAddonRootDescendantNodeMoved;
        AddonRoot.NewNodeIdRegistered -= OnAddonRootNewNodeIdRegistered;
        ((INotifyCollectionChanged)AddonRoot.CustomTags).CollectionChanged -= OnCustomTagsChanged;

        Utils.DisposeAndSetNull(ref _invalidHandler);

        _tagFilterDebounce.Cancel();
        _searchOptionsDebounce.Cancel();
        _searchTextDebounce.Cancel();

        CurrentNodes = EmptyNodes;
        CancelSearch();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        Deactivate();

        _tagFilterDebounce.Dispose();
        _searchOptionsDebounce.Dispose();
        _searchTextDebounce.Dispose();

        _selectedNodes.CollectionChanged -= OnSelectedNodesChanged;
        _selectedNodes.PropertyChanged -= OnSelectedNodesPropertyChanged;
        _movingNodes.CollectionChanged -= OnMovingNodesChanged;

        _selectedNodes.Dispose();
        _movingNodes.Dispose();

        CancelSearch();
        DisposeSortedNodes();
        ObserveCurrentGroup(null);
    }

    private void DisposeSortedNodes()
    {
        _sortedNodes?.Dispose();
        _sortedNodes = null;
    }

    private bool CanGotoParent() => IsValid && CurrentGroup != null;

    private bool CanOperateOnSelection() => IsValid && HasSelection;

    private bool CanMoveHere() => IsValid && _movingNodes.Count > 0;

    private bool CanOperateOnUpdateableSelection() => IsValid && _hasSelectedUpdateableAddon;

    private void NotifyCommandCanExecuteChanged()
    {
        GotoParentCommand.NotifyCanExecuteChanged();
        EnableCommand.NotifyCanExecuteChanged();
        DisableCommand.NotifyCanExecuteChanged();
        EnableRecursivelyCommand.NotifyCanExecuteChanged();
        DisableRecursivelyCommand.NotifyCanExecuteChanged();
        MoveCommand.NotifyCanExecuteChanged();
        MoveHereCommand.NotifyCanExecuteChanged();
        CancelMoveCommand.NotifyCanExecuteChanged();
        CreateRefAddonsBasedOnSelectedCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        SetAutoUpdateStrategyToDefaultRecursivelyCommand.NotifyCanExecuteChanged();
        SetAutoUpdateStrategyToEnabledRecursivelyCommand.NotifyCanExecuteChanged();
        SetAutoUpdateStrategyToDisabledRecursivelyCommand.NotifyCanExecuteChanged();
        OpenWorkshopPageCommand.NotifyCanExecuteChanged();
    }

    private void OnSelectedNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateSelectionDerivedValues();
    }

    private void OnSelectedNodesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // ObservableValidRefCollection also re-raises when an item becomes invalid.
        UpdateSelectionDerivedValues();
    }

    private void UpdateSelectionDerivedValues()
    {
        int count = _selectedNodes.Count;

        _selectedNode = _selectedNodes is [AddonNode selectedNode] ? selectedNode : null;
        OnPropertyChanged(nameof(SelectedNode));

        SetProperty(ref _hasSelection, count > 0, nameof(HasSelection));
        SetProperty(ref _isSingleSelection, count == 1, nameof(IsSingleSelection));
        SetProperty(ref _isMultipleSelection, count > 1, nameof(IsMultipleSelection));

        var names = count == 0 ? null : string.Join(", ", _selectedNodes.Select(node => node.Name));
        SetProperty(ref _selectionNames, names, nameof(SelectionNames));

        bool hasSelectedWorkshopVpkAddon = _selectedNodes
            .SelectMany(addon => addon.GetSelfAndDescendants())
            .OfType<WorkshopVpkAddon>()
            .Any();
        SetProperty(ref _hasSelectedUpdateableAddon, hasSelectedWorkshopVpkAddon, nameof(_hasSelectedUpdateableAddon));

        NotifyCommandCanExecuteChanged();
    }

    private void OnMovingNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var viewModels = new ObservableCollection<MovingNodeViewModel>(
            _movingNodes.Select(addon => new MovingNodeViewModel(this, addon)));
        _movingNodeViewModels = new ReadOnlyObservableCollection<MovingNodeViewModel>(viewModels);
        OnPropertyChanged(nameof(MovingNodeViewModels));
        NotifyCommandCanExecuteChanged();
    }

    private void ObserveCurrentGroup(AddonGroup? group)
    {
        if (_currentGroupObserved is not null)
        {
            _currentGroupObserved.PropertyChanged -= OnCurrentGroupPropertyChanged;
            _currentGroupObserved = null;
        }

        if (group is not null)
        {
            _currentGroupObserved = group;
            group.PropertyChanged += OnCurrentGroupPropertyChanged;
        }
    }

    private void OnCurrentGroupPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IValidity.IsValid))
        {
            return;
        }

        if (sender is not AddonGroup group || group.IsValid)
        {
            return;
        }

        // The current group became invalid: fall back to the nearest valid ancestor (or the root).
        AddonGroup? current = _currentGroup;
        while (true)
        {
            if (current == null)
            {
                GotoGroup(null);
                break;
            }

            if (current.IsValid)
            {
                GotoGroup(current);
                break;
            }

            current = current.Group;
        }
    }

    private void OnAddonRootDescendantNodeMoved(AddonNode node)
    {
        RefreshCurrentNodesDeferred();
    }

    private void OnAddonRootNewNodeIdRegistered(AddonNode node)
    {
        RefreshCurrentNodesDeferred();
    }

    private void OnCustomTagsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateExistingTags();
    }

    private IEnumerable<AddonNodeNavBarItemViewModel> BuildNavBarItemViewModels(AddonGroup? currentGroup)
    {
        var items = new List<AddonNodeNavBarItemViewModel>();
        AddonGroup? current = currentGroup;
        while (current != null)
        {
            items.Add(new AddonNodeNavBarItemViewModel(this, current));
            current = current.Group;
        }
        return items.ToArray().Reverse().ToArray();
    }

    private void MoveSelection()
    {
        this.ThrowIfInvalid();

        var selectedNodes = SelectedNodes.ToArray();
        foreach (var node in selectedNodes)
        {
            AddMovingNode(node);
        }
    }

    private async Task MoveSelectionHereAsync()
    {
        this.ThrowIfInvalid();

        var movingNodeViewModels = MovingNodeViewModels.ToArray();
        bool skipAll = false;
        foreach (var movingNodeViewModel in movingNodeViewModels)
        {
            try
            {
                movingNodeViewModel.MoveHere();
            }
            catch (Exception ex)
            {
                var nodePath = movingNodeViewModel.Addon?.NodePath ?? "null";
                Log.Error(ex, "Exception occurred during moving the AddonNode: {NodePath}", nodePath);
                if (movingNodeViewModels.Length == 1)
                {
                    await ShowMoveExceptionAsync(ex, nodePath);
                }
                else if (!skipAll)
                {
                    var reply = await ShowBatchMoveExceptionAsync(ex, nodePath);
                    if (reply == OperationInterruptReply.Abort)
                    {
                        break;
                    }
                    else if (reply == OperationInterruptReply.SkipAll)
                    {
                        skipAll = true;
                    }
                }
            }
        }
    }

    private async Task NewWorkshopCollectionAsync()
    {
        this.ThrowIfInvalid();

        if (AppServices.TryGetService<IWindowService>() is { } windowService)
        {
            windowService.OpenNewWorkshopCollectionWindow(_addonRoot, CurrentGroup);
        }
        await Task.CompletedTask;
    }

    // UI dispatch serializes production calls; headless callers can receive independent
    // debounce callbacks concurrently, so protect the refresh/generation transaction too.
    private readonly object _searchStateLock = new();

    private void RefreshCurrentNodes()
    {
        lock (_searchStateLock)
        {
            RefreshCurrentNodesCore();
        }
    }

    private void RefreshCurrentNodesCore()
    {
        if (!IsValid)
        {
            return;
        }

        var selectedNodes = SelectedNodes.ToArray();

        CurrentNodes = EmptyNodes;
        CancelSearch();

        var rawNodes = Search(CurrentGroup?.Children ?? AddonRoot.Nodes, out var startSearch);
        var sortMethod = SortMethod;
        ReadOnlyObservableCollection<AddonNode> nodes;
        if (sortMethod is AddonNodeSortMethod.None)
        {
            // Unsorted: hand the live collection straight through (it is filled asynchronously
            // when a search is running).
            DisposeSortedNodes();
            nodes = rawNodes;
        }
        else
        {
            // Sorted: the Avalonia VM used DynamicData's ToObservableChangeSet().Sort(...).Bind(out ...),
            // which stayed subscribed to the raw collection so async search hits streamed in sorted.
            // A snapshot copy here would drop every result that arrives after this line.
            DisposeSortedNodes();
            _sortedNodes = new SortedObservableCollection<AddonNode>(rawNodes, new AddonNodeComparer(sortMethod, IsAscendingOrder));
            nodes = new ReadOnlyObservableCollection<AddonNode>(_sortedNodes);
        }
        CurrentNodes = nodes;

        SelectNodes(selectedNodes);
        // Subscribe the live sorted collection BEFORE the worker can deliver its first hit.
        startSearch?.Invoke();
    }

    private async void RefreshCurrentNodesDeferred()
    {
        if (!IsValid)
        {
            return;
        }

        if (_uiDispatcher is { } dispatcher)
        {
            await dispatcher.InvokeAsync(() =>
            {
                if (IsValid)
                {
                    RefreshCurrentNodes();
                }
            });
        }
        else
        {
            RefreshCurrentNodes();
        }
    }

    private ReadOnlyObservableCollection<AddonNode> Search(ReadOnlyObservableCollection<AddonNode> nodes, out Action? startSearch)
    {
        startSearch = null;
        CancelSearch();

        if (SearchText.Length == 0 && !IsFilterEnabled && !SearchOptions.IsFlatten)
        {
            return nodes;
        }

        _searchCts = new();
        var cancellationToken = _searchCts.Token;
        var searchId = new object();
        _currentSearchId = searchId;
        IsSearching = true;

        var resultNodes = new ObservableCollection<AddonNode>();

        async void DoSearch()
        {
            void Consumer(AddonNode node)
            {
                void AddResult()
                {
                    lock (_searchStateLock)
                    {
                        if (!cancellationToken.IsCancellationRequested && _currentSearchId == searchId)
                            resultNodes.Add(node);
                    }
                }
                if (_uiDispatcher is { } dispatcher)
                {
                    dispatcher.Post(AddResult, UiDispatchPriority.Low);
                }
                else
                {
                    AddResult();
                }
            }

            try
            {
                await AddonNodeSearchUtils.SearchAsync(nodes, SearchText, SearchOptions, Consumer, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SearchAsync failed.");
                return;
            }

            lock (_searchStateLock)
            {
                if (_currentSearchId == searchId)
                {
                    // Keep the generation until cancellation: low-priority UI results may still be queued.
                    IsSearching = false;
                }
            }
        }

        startSearch = DoSearch;

        return new ReadOnlyObservableCollection<AddonNode>(resultNodes);
    }

    private void CancelSearch()
    {
        lock (_searchStateLock)
        {
            if (_searchCts is not null)
            {
                _searchCts.Cancel();
                _searchCts.Dispose();
                _searchCts = null;
            }
            _currentSearchId = null;
            IsSearching = false;
        }
    }

    private void ScheduleFilterTagsUpdate()
    {
        _tagFilterDebounce.Run(() =>
        {
            if (IsValid)
            {
                UpdateFilterTags();
            }
        });
    }

    private void ScheduleSearchOptionsUpdate()
    {
        _searchOptionsDebounce.Run(() =>
        {
            if (IsValid)
            {
                UpdateSearchOptions();
            }
        });
    }

    private void UpdateSearchOptions()
    {
        SearchOptions = new AddonNodeSearchOptions()
        {
            IgnoreCase = SearchIgnoreCase,
            IsFlatten = IsSearchFlatten,
            IsRegex = IsSearchRegex,
            Tags = FilterTags,
            TagFilterMode = TagFilterMode
        };
    }

    private void UpdateExistingTags()
    {
        string[] tags = [.. _addonRoot.CustomTags, .. AddonTags.BuiltInTags];
        ExistingTags = tags;
    }

    private void UpdateFilterTags()
    {
        if (IsFilterEnabled)
        {
            FilterTags = new HashSet<string>(SelectedTags);
        }
        else
        {
            FilterTags = null;
        }
    }

    private void UpdateTileViewMetrics()
    {
        var kind = ListItemViewKind;
        SetProperty(ref _isGridView, kind == AddonNodeListItemViewKind.Grid, nameof(IsGridView));
        SetProperty(ref _isTileView, kind.IsTile(), nameof(IsTileView));
        SetProperty(ref _tileViewSize, kind switch
        {
            AddonNodeListItemViewKind.MediumTile => MediumTileViewSize,
            AddonNodeListItemViewKind.LargeTile => LargeTileViewSize,
            AddonNodeListItemViewKind.SmallTile => SmallTileViewSize,
            _ => DefaultTileViewSize,
        }, nameof(TileViewSize));
    }

    private void SetSelectionEnabled(bool enabled)
    {
        this.ThrowIfInvalid();

        var selectedNodes = SelectedNodes.ToArray();
        foreach (var node in selectedNodes)
        {
            node.IsEnabled = enabled;
        }
    }

    private void SetSelectionEnabledRecursively(bool enabled)
    {
        this.ThrowIfInvalid();

        var selectedNodes = SelectedNodes.ToArray();
        foreach (var node in selectedNodes)
        {
            foreach (var node1 in node.GetSelfAndDescendantsByDfsPreorder())
            {
                node1.IsEnabled = enabled;
            }
        }
    }

    private void SetSelectionAutoUpdateStrategyRecursively(bool? strategy)
    {
        this.ThrowIfInvalid();

        var selectedNodes = SelectedNodes.ToArray();
        foreach (var addon in selectedNodes.SelectMany(addon => addon.GetSelfAndDescendants()))
        {
            if (addon is WorkshopVpkAddon workshopVpkAddon)
            {
                workshopVpkAddon.IsAutoUpdate = strategy;
            }
        }
    }

    private void OpenSelectionWorkshopPage()
    {
        this.ThrowIfInvalid();

        foreach (var addon in SelectedNodes.SelectMany(addon => addon.GetSelfAndDescendants()).OfType<WorkshopVpkAddon>())
        {
            if (addon.PublishedFileId is { } id)
            {
                Utils.OpenWorkshopPage(id);
            }
        }
    }

    private async Task DeleteSelectionAsync(bool retainFile)
    {
        this.ThrowIfInvalid();

        if (AppServices.TryGetService<IDialogService>() is { } dialogService)
        {
            string message = Texts.ConfirmDeleteMessage;
            if (retainFile)
            {
                message += '\n' + Texts.RetainFileMessage;
            }
            bool confirm = await dialogService.ConfirmAsync(message, Texts.ConfirmDelete);
            if (!confirm)
            {
                return;
            }
        }

        var selectedNodes = SelectedNodes.ToArray();
        int count = selectedNodes.Length;
        if (count == 0)
        {
            return;
        }
        var operations = new (Task, string)[count];
        for (int i = 0; i < count; i++)
        {
            var node = selectedNodes[i];
            var nodePath = node.NodePath;
            if (retainFile)
            {
                operations[i] = (node.DestroyAsync(), nodePath);
            }
            else
            {
                operations[i] = (node.DestroyWithFileAsync(), nodePath);
            }
        }

        if (AppServices.TryGetService<IWindowService>() is { } windowService)
        {
            windowService.ShowDeletionProgress(operations);
        }
    }

    internal async Task ShowMoveExceptionAsync(Exception exception, string nodePath)
    {
        if (AppServices.TryGetService<IDialogService>() is { } dialogService)
        {
            await dialogService.ShowExceptionAsync(exception, Texts.FailedToMoveItemWithName.FormatNoThrow(nodePath));
        }
    }

    internal async Task<OperationInterruptReply> ShowBatchMoveExceptionAsync(Exception exception, string nodePath)
    {
        if (AppServices.TryGetService<IDialogService>() is { } dialogService)
        {
            var message = MessageComposer.CombineMessageAndException(Texts.FailedToMoveItemWithName.FormatNoThrow(nodePath), exception);
            return await dialogService.ShowOperationInterruptAsync(message);
        }
        return OperationInterruptReply.Abort;
    }
}
