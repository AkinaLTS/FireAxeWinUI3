// Ported from FireAxe_Alt\FireAxe.GUI\Views\AddonNodeExplorerView.axaml.cs
// Differences that matter (also recorded in docs\phase-notes\P3-*.md):
//   * Avalonia ReactiveUI interactions (5) were already moved into the view model by P1 (IDialogService /
//     IWindowService), so this view registers nothing.
//   * DynamicResource AddonTileSize -> AddonTileMetrics.SetTileSize (see AddonNodeTileView).
//   * SelectedItems is not a dependency property in WinUI, so the list selection and
//     AddonNodeExplorerViewModel.SelectedNodes are synchronized by hand (both directions, re-entrancy guarded).
//   * Avalonia's GridSplitter -> a plain drag strip over column 2 (WinUI has none in the box, and
//     CommunityToolkit.WinUI.Controls.Sizers is not referenced by this project).
//   * Ctrl+F / Ctrl+X / Ctrl+V / Delete / F2 keep the Alt semantics, but Ctrl is tested as a key state
//     instead of Avalonia's KeyModifiers enum (no exact-match modifier API in WinUI).
using FireAxe.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Windows.System;
using Windows.UI.Core;

namespace FireAxe.Views;

/// <summary>The addon browser: breadcrumb + search + node list + detail pane.</summary>
public sealed partial class AddonNodeExplorerView : UserControl
{
    /// <summary>Width of the detail column when it is enabled (the Alt XAML hard-coded 400).</summary>
    private const double AddonNodeViewColumnWidth = 400;

    private const double MinAddonNodeViewColumnWidth = 200;

    private const double MinListColumnWidth = 300;

    public static readonly DependencyProperty IsSingleSelectionEnabledProperty =
        DependencyProperty.Register(nameof(IsSingleSelectionEnabled), typeof(bool), typeof(AddonNodeExplorerView), new PropertyMetadata(false));

    public static readonly DependencyProperty IsAddonNodeViewEnabledProperty =
        DependencyProperty.Register(nameof(IsAddonNodeViewEnabled), typeof(bool), typeof(AddonNodeExplorerView), new PropertyMetadata(true, OnIsAddonNodeViewEnabledChanged));

    public static readonly DependencyProperty IsWorkspaceActiveProperty =
        DependencyProperty.Register(nameof(IsWorkspaceActive), typeof(bool), typeof(AddonNodeExplorerView), new PropertyMetadata(true, OnIsWorkspaceActiveChanged));

    private AddonNodeExplorerViewModel? _viewModel;

    /// <summary>Guards the selection synchronization between the lists and the view model.</summary>
    private bool _syncingSelection;

    private bool _isDraggingSplitter;
    private double _splitterDragStartX;
    private double _splitterDragStartWidth;
    private double _detailWidth = AddonNodeViewColumnWidth;
    private bool _compactDetailsOpen;
    private bool IsCompactLayout => rootGrid.ActualWidth < 920;

    /// <summary>The window content this view attached its key fallback handler to, if any.</summary>
    private UIElement? _keyFallbackTarget;

    public AddonNodeExplorerView()
    {
        InitializeComponent();

        // Avalonia activated the view model through ReactiveUserControl<T>; the explorer is created by
        // MainWindowViewModel and never activated there, so the binder is the only activation path.
        _ = new Controls.ViewModelLifecycleBinder(this);

        DataContextChanged += OnDataContextChanged;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>When true the list allows a single selection (Avalonia <c>SelectionMode.Single</c>).</summary>
    public bool IsSingleSelectionEnabled
    {
        get => (bool)GetValue(IsSingleSelectionEnabledProperty);
        set => SetValue(IsSingleSelectionEnabledProperty, value);
    }

    /// <summary>Hides the splitter and the detail pane when false.</summary>
    public bool IsAddonNodeViewEnabled
    {
        get => (bool)GetValue(IsAddonNodeViewEnabledProperty);
        set => SetValue(IsAddonNodeViewEnabledProperty, value);
    }

    /// <summary>The view model currently bound through DataContext.</summary>
    public AddonNodeExplorerViewModel? ViewModel => _viewModel;

    /// <summary>
    /// False while the explorer is mounted on a hidden navigation page. The view stays loaded (the
    /// navigation keeps every workspace alive), so <see cref="IsLoaded"/> alone cannot tell whether its
    /// window-wide key handlers would steal typing from another page.
    /// </summary>
    public bool IsWorkspaceActive
    {
        get => (bool)GetValue(IsWorkspaceActiveProperty);
        set => SetValue(IsWorkspaceActiveProperty, value);
    }

    private static void OnIsWorkspaceActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (AddonNodeExplorerView)d;
        if ((bool)e.NewValue)
        {
            view.AttachKeyFallback();
        }
        else
        {
            view.DetachKeyFallback();
            view.EndSplitterDrag();
        }
    }

    private static void OnIsAddonNodeViewEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((AddonNodeExplorerView)d).ApplyAddonNodeViewEnabled((bool)e.NewValue);
    }

    private void ApplyAddonNodeViewEnabled(bool enabled)
    {
        if (detailPane is null) return;
        var compact = IsCompactLayout;
        // Only the compact overlay needs an opaque surface; the docked pane shares Mica.
        compactDetailBackground.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        // Keep list and details in separate layout slots, including compact mode.
        addonNodeViewColumn.Width = new GridLength(enabled && !compact ? _detailWidth : 0);
        Grid.SetRow(detailPane, compact ? 2 : 0);
        Grid.SetRowSpan(detailPane, compact ? 1 : 3);
        splitter.Visibility = enabled && !compact ? Visibility.Visible : Visibility.Collapsed;
        compactDetailsButton.Visibility = enabled && compact ? Visibility.Visible : Visibility.Collapsed;
        closeCompactDetailsButton.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        detailPane.Visibility = enabled && (!compact || _compactDetailsOpen) ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumn(detailPane, compact ? 0 : 2);
        Grid.SetColumnSpan(detailPane, compact ? 3 : 1);
        Canvas.SetZIndex(detailPane, compact ? 10 : 0);
    }

    private void OnExplorerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        EndSplitterDrag();
        ApplyAddonNodeViewEnabled(IsAddonNodeViewEnabled);
    }

    private void OnToggleCompactDetails(object sender, RoutedEventArgs e)
    {
        _compactDetailsOpen = !_compactDetailsOpen;
        ApplyAddonNodeViewEnabled(IsAddonNodeViewEnabled);
        if (!_compactDetailsOpen) FocusCurrentList();
    }

    /// <summary>
    /// Adds the key handling to the window content root as a fallback.
    /// </summary>
    /// <remarks>
    /// Avalonia delivered the explorer's key bindings as soon as the explorer had focus, and the explorer
    /// could always keep that focus. In WinUI the focus escapes the explorer on its own — clearing the
    /// selection drops the focused element and WinUI hands the focus to the first focusable control of the
    /// window (measured: the "检查" button of the main window status bar) — and the explorer then never sees
    /// Ctrl+X / Ctrl+V / Ctrl+F / Delete / F2 again. Registering the same handler on the window content root
    /// keeps them working while still letting a TextBox consume its own editing keys, because those mark the
    /// event as handled and this handler is not registered for handled events.
    /// </remarks>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachKeyFallback();
        ApplyAddonNodeViewEnabled(IsAddonNodeViewEnabled);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachKeyFallback();
    }

    private void AttachKeyFallback()
    {
        if (_keyFallbackTarget is null && IsWorkspaceActive && XamlRoot?.Content is UIElement content)
        {
            content.AddHandler(KeyDownEvent, new KeyEventHandler(OnKeyFallbackKeyDown), handledEventsToo: false);
            _keyFallbackTarget = content;
        }
    }

    private void DetachKeyFallback()
    {
        if (_keyFallbackTarget is not null)
        {
            _keyFallbackTarget.RemoveHandler(KeyDownEvent, new KeyEventHandler(OnKeyFallbackKeyDown));
            _keyFallbackTarget = null;
        }
    }

    private void OnKeyFallbackKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Already handled by the element under the focus (a TextBox cutting/pasting text, for example).
        if (e.Handled || !IsWorkspaceActive)
        {
            return;
        }

        OnKeyDown(sender, e);
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (_viewModel is { } oldViewModel)
        {
            oldViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            oldViewModel.SelectedNodes.CollectionChanged -= OnSelectedNodesChanged;
        }

        _viewModel = args.NewValue as AddonNodeExplorerViewModel;

        if (_viewModel is { } newViewModel)
        {
            newViewModel.PropertyChanged += OnViewModelPropertyChanged;
            newViewModel.SelectedNodes.CollectionChanged += OnSelectedNodesChanged;
        }

        SyncSelectionMode();
        ApplyTileViewSize();
        UpdateAddonNodeView();
        ApplySelectionToLists();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is not { } viewModel)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(AddonNodeExplorerViewModel.CurrentGroup):
                // Alt: WhenAnyValue(CurrentGroup).Subscribe(_ => Focus())
                // Focusing the UserControl itself is not enough in WinUI: after a double click the focus
                // can end up on the window, so the Ctrl+X / Ctrl+V / Ctrl+F / Delete / F2 handling in
                // OnKeyDown never sees the key (measured: Ctrl+F did nothing until a child got focus).
                // Focusing the visible list keeps the keyboard focus inside this subtree, so the keys
                // bubble up to OnKeyDown again.
                FocusCurrentList();

                // The selection is cleared while the group changes, and dropping the focused element lets
                // WinUI move the focus to the first focusable control of the window (measured: the "检查"
                // button of the main window's status bar), which is outside this subtree and therefore
                // swallows Ctrl+X / Ctrl+V / Ctrl+F / Delete / F2. Re-apply the focus once the layout and
                // the selection have settled.
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, FocusCurrentList);
                break;
            case nameof(AddonNodeExplorerViewModel.TileViewSize):
                ApplyTileViewSize();
                break;
            case nameof(AddonNodeExplorerViewModel.SelectedNode):
                UpdateAddonNodeView();
                break;
            case nameof(AddonNodeExplorerViewModel.IsSingleSelection):
                SyncSelectionMode();
                break;
            default:
                break;
        }
    }

    private void ApplyTileViewSize()
    {
        if (_viewModel is { } viewModel)
        {
            Controls.AddonTileMetrics.SetTileSize(viewModel.TileViewSize);
        }
    }

    /// <summary>
    /// Puts the keyboard focus on the list that is currently visible so that the accelerators handled in
    /// <see cref="OnKeyDown"/> keep working after a navigation.
    /// </summary>
    private void FocusCurrentList()
    {
        var listView = tileListView.Visibility == Visibility.Visible ? tileListView : gridListView;
        if (!listView.Focus(FocusState.Programmatic))
        {
            Focus(FocusState.Programmatic);
        }
    }

    private void SyncSelectionMode()
    {
        // AddonNodeExplorerViewModel.IsSingleSelection is derived from the selection collection, and the
        // lists are the only source of selection changes, so mirror the Alt DP rather than the property.
        var selectionMode = IsSingleSelectionEnabled ? ListViewSelectionMode.Single : ListViewSelectionMode.Extended;
        if (tileListView.SelectionMode != selectionMode)
        {
            tileListView.SelectionMode = selectionMode;
        }

        if (gridListView.SelectionMode != selectionMode)
        {
            gridListView.SelectionMode = selectionMode;
        }
    }

    private void UpdateAddonNodeView()
    {
        // Alt bound DataContext="{Binding SelectedNode, Converter=ToViewModel}" -- a fresh view model per
        // selection, which is exactly what AddonNodeViewModel.Create does.
        if (IsCompactLayout && IsAddonNodeViewEnabled && _viewModel?.SelectedNode is not null)
        {
            _compactDetailsOpen = true;
        }
        ApplyAddonNodeViewEnabled(IsAddonNodeViewEnabled);
        addonNodeView.DataContext = _viewModel?.SelectedNode is { } node
            ? AddonNodeViewModel.Create(node)
            : null;
    }

    private void OnSelectedNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ApplySelectionToLists();
    }

    private void OnListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection || _viewModel is not { } viewModel)
        {
            return;
        }

        var selectedNodes = ((ListView)sender).SelectedItems.OfType<AddonNode>().ToArray();

        _syncingSelection = true;
        try
        {
            viewModel.SelectNodes(selectedNodes);
        }
        finally
        {
            _syncingSelection = false;
        }

        ApplySelectionToLists();
    }

    /// <summary>Pushes <see cref="AddonNodeExplorerViewModel.SelectedNodes"/> into both lists.</summary>
    private void ApplySelectionToLists()
    {
        if (_syncingSelection || _viewModel is not { } viewModel)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            foreach (var listView in new[] { tileListView, gridListView })
            {
                var desired = viewModel.SelectedNodes.Where(node => listView.Items.Contains(node)).ToArray();
                var current = listView.SelectedItems.OfType<AddonNode>().ToArray();

                if (desired.Length == current.Length && desired.SequenceEqual(current))
                {
                    continue;
                }

                listView.SelectedItems.Clear();
                foreach (var node in desired)
                {
                    listView.SelectedItems.Add(node);
                }
            }
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    // ============================ tool bar ============================

    private void OnGotoParentButtonClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.GotoParent();
    }

    private void OnGotoRootButtonClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.GotoGroup(null);
    }

    private void OnClearSearchTextButtonClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not { } viewModel) return;
        viewModel.ClearSearchText();
        viewModel.IsFilterEnabled = false;
        viewModel.IsSearchFlatten = false;
        viewModel.IsSearchRegex = false;
        viewModel.SelectedTags = Array.Empty<string>();
    }

    private void OnToggleOrderDirectionButtonClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.ToggleOrderDirection();
    }

    private void OnSearchOptionsButtonClick(object sender, RoutedEventArgs e)
    {
        searchOptionsControl.Visibility = searchOptionsControl.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    // ============================ context menu ============================

    private void OnRefreshMenuClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.RefreshCommand.Execute(null);
    }

    private void OnNewGroupMenuClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.NewGroupCommand.Execute(null);
    }

    private void OnNewRefAddonMenuClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.NewRefAddonCommand.Execute(null);
    }

    private void OnNewWorkshopAddonMenuClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.NewWorkshopAddonCommand.Execute(null);
    }

    private void OnNewWorkshopCollectionMenuClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.NewWorkshopCollectionCommand.Execute(null);
    }

    private void OnCreateRefAddonsBasedOnSelectedMenuClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.CreateRefAddonsBasedOnSelectedCommand.Execute(null);
    }

    private void OnEnableMenuClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.EnableCommand.Execute(null);
    }

    private void OnDisableMenuClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.DisableCommand.Execute(null);
    }

    private void OnEnableRecursivelyMenuClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.EnableRecursivelyCommand.Execute(null);
    }

    private void OnDisableRecursivelyMenuClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.DisableRecursivelyCommand.Execute(null);
    }

    private void OnMoveMenuClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.MoveCommand.Execute(null);
    }

    private void OnMoveHereMenuClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.MoveHereCommand.Execute(null);
    }

    private void OnCancelMoveMenuClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.CancelMoveCommand.Execute(null);
    }

    private void OnDeleteMenuClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.DeleteCommand.Execute(false);
    }

    private void OnDeleteAndRetainFileMenuClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.DeleteCommand.Execute(true);
    }

    private void OnSetAutoUpdateStrategyToDefaultRecursivelyMenuClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.SetAutoUpdateStrategyToDefaultRecursivelyCommand.Execute(null);
    }

    private void OnSetAutoUpdateStrategyToEnabledRecursivelyMenuClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.SetAutoUpdateStrategyToEnabledRecursivelyCommand.Execute(null);
    }

    private void OnSetAutoUpdateStrategyToDisabledRecursivelyMenuClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.SetAutoUpdateStrategyToDisabledRecursivelyCommand.Execute(null);
    }

    private void OnOpenWorkshopPageMenuClick(object sender, RoutedEventArgs e)
    {
        _viewModel?.OpenWorkshopPageCommand.Execute(null);
    }

    // ============================ moving bar ============================

    private void OnRemoveMovingNodeButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AddonNodeExplorerViewModel.MovingNodeViewModel movingNode })
        {
            movingNode.Remove();
        }
    }

    // ============================ list interaction ============================

    private void OnItemsDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_viewModel is not { } viewModel || sender is not ListView listView)
        {
            return;
        }

        // Alt walked the logical ancestor chain looking for DataContext is AddonNode. WinUI has no logical
        // tree, so the visual ancestor walk finds the ListViewItem that owns the tapped element, which also
        // guarantees the hit came from the list itself.
        var item = FindAncestor<ListViewItem>(e.OriginalSource as DependencyObject);

        switch (item?.Content)
        {
            case AddonGroup addonGroup:
                viewModel.GotoGroup(addonGroup);
                e.Handled = true;
                break;
            case RefAddonNode { ActualSourceAddon: AddonGroup sourceAddonGroup }:
                viewModel.GotoGroup(sourceAddonGroup);
                e.Handled = true;
                break;
            default:
                break;
        }
    }

    private static T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        EndSplitterDrag();

        // Alt: e.InitialPressMouseButton == MouseButton.XButton1 -> GotoParent()
        if (!e.Handled && IsWorkspaceActive
            && e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.XButton1Released
            && _viewModel is { } viewModel && viewModel.GotoParentCommand.CanExecute(null))
        {
            viewModel.GotoParent();
            e.Handled = true;
        }
    }

    private async void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!IsWorkspaceActive || _viewModel is not { } viewModel)
        {
            return;
        }

        // Text editing owns cut/paste/delete/F2. Focus can be on an editor's template child.
        for (DependencyObject? focused = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
             focused is not null; focused = VisualTreeHelper.GetParent(focused))
        {
            if (focused is TextBox or PasswordBox or RichEditBox) return;
        }

        bool isControlDown = IsKeyDown(VirtualKey.Control);
        bool isShiftDown = IsKeyDown(VirtualKey.Shift);
        bool isMenuDown = IsKeyDown(VirtualKey.Menu);

        // Alt matched KeyModifiers == KeyModifiers.Control exactly.
        if (isControlDown && !isShiftDown && !isMenuDown)
        {
            switch (e.Key)
            {
                case VirtualKey.X when viewModel.MoveCommand.CanExecute(null):
                    e.Handled = true;
                    viewModel.MoveCommand.Execute(null);
                    break;
                case VirtualKey.V when viewModel.MoveHereCommand.CanExecute(null):
                    e.Handled = true;
                    await viewModel.MoveHereCommand.ExecuteAsync(null);
                    break;
                case VirtualKey.F:
                    e.Handled = true;
                    searchTextBox.Focus(FocusState.Programmatic);
                    break;
                default:
                    break;
            }

            return;
        }

        if (isControlDown || isShiftDown || isMenuDown)
        {
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.Delete when viewModel.DeleteCommand.CanExecute(false):
                e.Handled = true;
                await viewModel.DeleteCommand.ExecuteAsync(false);
                break;
            case VirtualKey.F2:
                e.Handled = true;

                // Alt: addonNodeView.IsEffectivelyVisible -- the detail pane only has a name to edit when a
                // single node is selected.
                if (IsAddonNodeViewEnabled && viewModel.SelectedNode is not null && addonNodeView.NameEditingControl is { } nameEditingControl)
                {
                    _compactDetailsOpen = IsCompactLayout;
                    ApplyAddonNodeViewEnabled(IsAddonNodeViewEnabled);
                    nameEditingControl.StartEditing();
                }

                break;
            default:
                break;
        }
    }

    private static bool IsKeyDown(VirtualKey key)
    {
        var state = InputKeyboardSource.GetKeyStateForCurrentThread(key);
        return (state & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    }

    // ============================ splitter ============================

    private void OnSplitterPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // Only the press is bound to the 4 dip wide strip; the move/release tracking runs at the
        // UserControl level (OnRootPointerMoved / OnPointerReleased) because pointer capture on such a
        // thin Border proved unreliable — moves were silently dropped and the column never resized.
        _isDraggingSplitter = true;
        _splitterDragStartX = e.GetCurrentPoint(rootGrid).Position.X;
        _splitterDragStartWidth = addonNodeViewColumn.ActualWidth;
        splitter.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnRootPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingSplitter)
        {
            return;
        }

        double delta = e.GetCurrentPoint(rootGrid).Position.X - _splitterDragStartX;
        double maxWidth = Math.Max(MinAddonNodeViewColumnWidth, rootGrid.ActualWidth - MinListColumnWidth);
        double width = Math.Clamp(_splitterDragStartWidth - delta, MinAddonNodeViewColumnWidth, maxWidth);
        _detailWidth = width;
        addonNodeViewColumn.Width = new GridLength(width);
        e.Handled = true;
    }

    private void EndSplitterDrag()
    {
        _isDraggingSplitter = false;
    }
}
