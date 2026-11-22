// Ported from FireAxe_Alt\FireAxe.GUI\Views\MainWindow.axaml.cs
//   this.WhenAnyValue(x => x.ViewModel) + WhenActivated   -> constructor + RootGrid.Loaded
//   this.WhenAnyValue(x => x.Width/Height/WindowState)    -> AppWindow.Changed (physical px / RasterizationScale)
//   ReactiveWindow<TViewModel>                            -> plain Window with a ViewModel property
//   RegisterViewModelConnection + 25 Interaction handlers -> gone (IDialogService / IPickerService /
//                                                           IWindowService are called from the ViewModel)
using System;
using System.ComponentModel;
using System.Linq;
using FireAxe.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using Serilog;

namespace FireAxe.Views;

public sealed partial class MainWindow : Window
{
    /// <summary>Guards the write-back while the saved geometry is being applied.</summary>
    private bool _isApplyingGeometry = false;

    private bool _isClosed = false;

    public MainWindow(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        // Must be assigned before InitializeComponent: the x:Bind generated code reads it.
        ViewModel = viewModel;
        InitializeComponent();
        // The XAML backdrop API manages activation, theme changes and the system fallback
        // when Mica is unavailable or transparency effects are disabled.
        SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
        AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;

        Title = XBind.WindowTitle(viewModel.TitleExtraInfo);
        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        RootGrid.Loaded += OnRootGridLoaded;
        RootGrid.AddHandler(UIElement.PointerReleasedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(OnNavigationPointerReleased), true);
        AppWindow.Changed += OnAppWindowChanged;
        // Window.Closed (AppWindow has no Closed event in WinAppSDK 2.6.5 — only Closing/Changed/Destroying).
        Closed += (sender, args) => OnAppWindowClosed();
    }

    public MainWindowViewModel ViewModel { get; }

    private readonly System.Collections.Generic.List<HostedPage> _pageStack = [];
    private readonly System.Collections.Generic.Dictionary<Type, HostedPage> _cachedPages = [];
    private string _primaryPage = "library";

    public HostedPage? FindCachedPage(Type type) => _cachedPages.GetValueOrDefault(type);

    public bool HasBlockingHostedPage => _pageStack.Exists(page => !page.CanClose);

    public void ShowLibraryPage()
    {
        if (HasBlockingHostedPage)
        {
            return;
        }
        _primaryPage = "library";
        _restoringHistory = true;
        try
        {
            if (!ReferenceEquals(MainNavigation.SelectedItem, LibraryNavigationItem))
            {
                MainNavigation.SelectedItem = LibraryNavigationItem;
            }
        }
        finally
        {
            _restoringHistory = false;
        }
        ReturnToPrimaryPage();
        ApplyPrimaryVisibility();
        RenderHostedPage();
    }

    public void ShowHostedPage(HostedPage page, bool cache = false)
    {
        if (HasBlockingHostedPage && page is not OperationsProgressWindow)
        {
            // Existing cached pages retain their session; newly created temporary pages must resolve callers.
            if (!_cachedPages.ContainsValue(page) && !_pageStack.Contains(page)) page.CompleteClose();
            return;
        }
        if (page.PendingClose || page.IsClosed)
        {
            _cachedPages.Remove(page.GetType());
            page.CompleteClose();
            return;
        }
        if (_pageStack.Contains(page)) _pageStack.Remove(page);
        if (cache) _cachedPages[page.GetType()] = page;
        page.CloseRequested -= OnHostedCloseRequested;
        page.CloseRequested += OnHostedCloseRequested;
        _pageStack.Add(page);
        RenderHostedPage();
    }

    private void OnHostedCloseRequested(HostedPage page)
    {
        _pageStack.Remove(page);
        _cachedPages.Remove(page.GetType());
        SecondaryHost.Content = null;
        page.CloseRequested -= OnHostedCloseRequested;
        page.CompleteClose();
        RenderHostedPage();
    }

    private void OnHostBackRequested(Microsoft.UI.Xaml.Controls.NavigationView sender,
        Microsoft.UI.Xaml.Controls.NavigationViewBackRequestedEventArgs args)
    {
        TryNavigateHostedBack();
    }

    private void OnBackKeyboardAcceleratorInvoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender,
        Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        if (TryNavigateHostedBack())
        {
            args.Handled = true;
        }
    }

    private bool TryNavigateHostedBack()
    {
        if (_pageStack.Count == 0) return false;
        var page = _pageStack[^1];
        if (!page.CanClose) return true;
        _pageStack.RemoveAt(_pageStack.Count - 1);
        SecondaryHost.Content = null;
        if (!_cachedPages.ContainsValue(page))
        {
            page.CloseRequested -= OnHostedCloseRequested;
            page.CompleteClose();
        }
        RenderHostedPage();
        return true;
    }

    private void ReturnToPrimaryPage()
    {
        if (_pageStack.Exists(page => !page.CanClose)) return;
        SecondaryHost.Content = null;
        foreach (var page in _pageStack.ToArray())
        {
            if (_cachedPages.ContainsValue(page)) continue;
            page.CloseRequested -= OnHostedCloseRequested;
            page.CompleteClose();
        }
        _pageStack.Clear();
        RenderHostedPage();
    }
    public void ClearHostedPages(bool force = false)
    {
        if (!force && HasBlockingHostedPage)
        {
            foreach (var page in _cachedPages.Values.ToArray())
            {
                if (!page.CanClose || _pageStack.Contains(page)) continue;
                page.CloseRequested -= OnHostedCloseRequested;
                page.CompleteClose();
                _cachedPages.Remove(page.GetType());
            }
            return;
        }
        if (!_isClosed) SecondaryHost.Content = null;
        foreach (var page in System.Linq.Enumerable.Distinct(System.Linq.Enumerable.Concat(_pageStack, _cachedPages.Values)))
        {
            page.CloseRequested -= OnHostedCloseRequested;
            page.CompleteClose();
        }
        _pageStack.Clear();
        _cachedPages.Clear();
        _history.Clear(); _historyIndex = -1;
        if (!_isClosed) RenderHostedPage();
    }

    private sealed record NavigationState(string Primary, HostedPage[] Pages, string? Category);
    private readonly System.Collections.Generic.List<NavigationState> _history = [];
    private int _historyIndex = -1;
    private bool _restoringHistory;

    private void CaptureNavigation()
    {
        if (_restoringHistory || _isClosed) return;
        var state = new NavigationState(_primaryPage, _pageStack.ToArray(), (_pageStack.LastOrDefault() as AppSettingsWindow)?.SelectedCategory);
        if (_historyIndex >= 0)
        {
            var previous = _history[_historyIndex];
            if (previous.Primary == state.Primary && previous.Category == state.Category && previous.Pages.SequenceEqual(state.Pages)) return;
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        }
        _history.Add(state);
        _historyIndex = _history.Count - 1;
    }

    private void OnNavigationPointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var properties = e.GetCurrentPoint(RootGrid).Properties;
        bool back = properties.PointerUpdateKind == Microsoft.UI.Input.PointerUpdateKind.XButton1Released;
        bool forward = properties.PointerUpdateKind == Microsoft.UI.Input.PointerUpdateKind.XButton2Released;
        if (!back && !forward) return;
        // Explorer owns group back/forward while visible; handled events must not navigate twice.
        if (e.Handled) return;

        e.Handled = true;
        NavigateHistory(back ? -1 : 1);
    }

    private void NavigateHistory(int direction)
    {
        if (_pageStack.Exists(page => !page.CanClose)) return;
        int next = _historyIndex + direction;
        while (next >= 0 && next < _history.Count && _history[next].Pages.Any(page => page.IsClosed)) next += direction;
        if (next < 0 || next >= _history.Count) return;
        var state = _history[next];
        _restoringHistory = true;
        try
        {
            foreach (var old in _pageStack.Where(page => !state.Pages.Contains(page) && !_cachedPages.ContainsValue(page)).ToArray()) old.CompleteClose();
            _primaryPage = state.Primary;
            foreach (var item in MainNavigation.MenuItems.Concat(MainNavigation.FooterMenuItems))
                if (item is Microsoft.UI.Xaml.Controls.NavigationViewItem nav && (string?)nav.Tag == state.Primary) MainNavigation.SelectedItem = nav;
            _pageStack.Clear();
            _pageStack.AddRange(state.Pages);
            if (_pageStack.LastOrDefault() is AppSettingsWindow settings && state.Category is not null) settings.SelectCategory(state.Category);
            _historyIndex = next;
            ApplyPrimaryVisibility();
            RenderHostedPage();
        }
        finally { _restoringHistory = false; }
    }

    private void RenderHostedPage()
    {
        var page = _pageStack.Count == 0 ? null : _pageStack[^1];
        SecondaryHost.Content = page;
        SecondaryHost.Visibility = page is null ? Visibility.Collapsed : Visibility.Visible;
        PrimaryPages.Visibility = page is null ? Visibility.Visible : Visibility.Collapsed;
        MainNavigation.IsBackButtonVisible = page is not null
            ? Microsoft.UI.Xaml.Controls.NavigationViewBackButtonVisible.Visible
            : Microsoft.UI.Xaml.Controls.NavigationViewBackButtonVisible.Collapsed;
        MainNavigation.IsBackEnabled = page is not null;
        // Rendering must not write SelectedItem: NavigationView may deliver selection events
        // after this method returns, creating a navigation/reload feedback loop.
        CaptureNavigation();
        if (page is AppSettingsWindow settings) { settings.CategoryChanged -= OnSettingsCategoryChanged; settings.CategoryChanged += OnSettingsCategoryChanged; }
        LibraryExplorer.IsWorkspaceActive = page is null && _primaryPage == "library";
    }
    private void ApplyPrimaryVisibility()
    {
        LibraryPage.Visibility = _primaryPage == "library" ? Visibility.Visible : Visibility.Collapsed;
        ToolsPage.Visibility = _primaryPage == "tools" ? Visibility.Visible : Visibility.Collapsed;
        DownloadsPage.Visibility = _primaryPage == "downloads" ? Visibility.Visible : Visibility.Collapsed;
        HelpPage.Visibility = _primaryPage == "help" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSettingsCategoryChanged(object? sender, EventArgs e) => CaptureNavigation();
    private void OnNavigationSelectionChanged(Microsoft.UI.Xaml.Controls.NavigationView sender,
        Microsoft.UI.Xaml.Controls.NavigationViewSelectionChangedEventArgs args)
    {
        // Selection can change while InitializeComponent is still constructing the content.
        if (LibraryPage is null || _restoringHistory) return;
        var page = (args.SelectedItem as Microsoft.UI.Xaml.Controls.NavigationViewItem)?.Tag as string;
        if (_pageStack.Exists(current => !current.CanClose))
        {
            foreach (var item in System.Linq.Enumerable.Concat(sender.MenuItems, sender.FooterMenuItems))
            {
                if (item is Microsoft.UI.Xaml.Controls.NavigationViewItem navigationItem && (string?)navigationItem.Tag == _primaryPage)
                {
                    if (!ReferenceEquals(sender.SelectedItem, navigationItem)) sender.SelectedItem = navigationItem;
                    break;
                }
            }
            return;
        }
        if (page == "settings") return; // ItemInvoked opens the hosted settings page.
        if (page is not ("library" or "tools" or "downloads" or "help")) return;
        _primaryPage = page;
        ReturnToPrimaryPage();
        ApplyPrimaryVisibility();
        // The explorer stays mounted, but its root-level keyboard fallback must only run on its page.
        RenderHostedPage();
    }

    private void OnNavigationItemInvoked(Microsoft.UI.Xaml.Controls.NavigationView sender,
        Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs args)
    {
        var tag = (args.InvokedItemContainer as Microsoft.UI.Xaml.Controls.NavigationViewItem)?.Tag as string;
        if (tag is "library" or "tools" or "downloads" or "help") ReturnToPrimaryPage();
        if (tag == "settings"
            && ViewModel.OpenSettingsWindowCommand.CanExecute(null))
        {
            ViewModel.OpenSettingsWindowCommand.Execute(null);
        }
    }

    private void OnRootGridLoaded(object sender, RoutedEventArgs e)
    {
        ApplySavedGeometry();

        // P1 moved the ViewModel lifecycle to the View (was IActivatableViewModel/WhenActivated);
        // this is the "the main window is live" moment the Avalonia version used.
        ViewModel.InitIfNot();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The app closes the windows before it disposes the services, and MainWindowViewModel.Dispose()
        // clears AddonRoot — which raises PropertyChanged after the WinUI window object is already dead
        // (0x800710DD "operation identifier is not valid" if we touch Title then).
        if (_isClosed)
        {
            return;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.AddonRoot)) ClearHostedPages();
        if (e.PropertyName == nameof(MainWindowViewModel.TitleExtraInfo))
        {
            Title = XBind.WindowTitle(ViewModel.TitleExtraInfo);
        }
    }

    private void OnAppWindowClosed()
    {
        _isClosed = true;
        ClearHostedPages(force: true);
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        AppWindow.Changed -= OnAppWindowChanged;
    }

    private void ApplySavedGeometry()
    {
        var settings = ViewModel.AppSettings;
        double scale = GetRasterizationScale();

        _isApplyingGeometry = true;
        try
        {
            var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
            var frameWidth = Math.Max(0, AppWindow.Size.Width - AppWindow.ClientSize.Width);
            var frameHeight = Math.Max(0, AppWindow.Size.Height - AppWindow.ClientSize.Height);
            var size = WindowGeometry.GetClientSize(settings.MainWindowWidth, settings.MainWindowHeight,
                scale, workArea.Width - frameWidth, workArea.Height - frameHeight);
            AppWindow.ResizeClient(new SizeInt32(size.Width, size.Height));
            // MainWindow is the only native window after P6. Center its restored bounds on its display.
            AppWindow.Move(new PointInt32(workArea.X + (workArea.Width - AppWindow.Size.Width) / 2,
                workArea.Y + (workArea.Height - AppWindow.Size.Height) / 2));

            if (settings.IsMainWindowMaximized && AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.Maximize();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during applying the saved main window geometry.");
        }
        finally
        {
            _isApplyingGeometry = false;
        }
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (_isApplyingGeometry)
        {
            return;
        }

        var settings = ViewModel.AppSettings;
        double scale = GetRasterizationScale();

        try
        {
            if (args.DidPresenterChange)
            {
                settings.IsMainWindowMaximized =
                    sender.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized };
            }

            // Same as the Avalonia version: only remember the size while the window is not maximized.
            if (args.DidSizeChange
                && sender.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored })
            {
                var clientSize = sender.ClientSize;
                if (clientSize.Width > 0 && clientSize.Height > 0 && scale > 0)
                {
                    settings.MainWindowWidth = clientSize.Width / scale;
                    settings.MainWindowHeight = clientSize.Height / scale;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during tracking the main window geometry.");
        }
    }

    /// <summary>
    /// The window's DPI scale. <see cref="AppWindow"/> works in physical pixels while
    /// <see cref="AppSettings.MainWindowWidth"/> stores device-independent units (as in the Avalonia version).
    /// </summary>
    private double GetRasterizationScale()
    {
        double scale = RootGrid.XamlRoot?.RasterizationScale ?? 0;
        return scale > 0 ? scale : 1.0;
    }
}
