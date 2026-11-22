// Ported from FireAxe_Alt\FireAxe.GUI\AppWindowManager.cs + WindowReference.cs
//   * Avalonia Window { DataContext = vm; Show(); Activate(); }  -> WinUI Window { ViewModel = vm; Activate(); }
//   * Window.Closed lifetime tracking (WindowReference<T>)       -> kept, on WinUI Window.Closed
//   * "the app exits when the last window closes"               -> the main window is kept in a field and its
//     AppWindow.Closing is turned into the explicit shutdown sequence (see App.xaml.cs).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;
using FireAxe.Resources;
using FireAxe.ViewModels;
using FireAxe.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace FireAxe.Services;

/// <summary>
/// <see cref="IWindowService"/> over WinUI 3 <see cref="Window"/>s, and the app's
/// <see cref="IActiveWindowProvider"/>.
/// </summary>
public sealed class WindowService : IWindowService, IActiveWindowProvider
{
    private readonly IServiceProvider _services;
    private readonly WinUiAppThemeService _themeService;

    private readonly List<WeakReference<Window>> _openWindows = [];

    private MainWindow? _mainWindow = null;
    private Window? _activeWindow = null;

    private readonly string _iconPath = Path.Join(AppContext.BaseDirectory, "Assets", "AppLogo.ico");

    public WindowService(IServiceProvider services, WinUiAppThemeService themeService)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(themeService);
        _services = services;
        _themeService = themeService;
    }

    public Window? ActiveWindow => _activeWindow ?? MainWindow;

    public MainWindow? MainWindow => _mainWindow;

    public MainWindowViewModel? MainWindowViewModel { get; private set; } = null;

    private AppSettings AppSettings => _services.GetRequiredService<AppSettings>();

    // ---------------------------------------------------------------- main window

    public MainWindow CreateMainWindow(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        if (_mainWindow is not null)
        {
            throw new InvalidOperationException("The main window already exists.");
        }

        MainWindowViewModel = viewModel;
        _mainWindow = new MainWindow(viewModel);
        Attach(_mainWindow);
        return _mainWindow;
    }

    public bool HasBlockingHostedPage => _mainWindow?.HasBlockingHostedPage == true;

    public void ActivateMainWindow()
    {
        if (_mainWindow is not { } window)
        {
            return;
        }

        if (window.AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
        {
            presenter.Restore();
        }
        window.AppWindow.Show();
        window.Activate();
    }

    public void ShowLibraryPage()
    {
        _mainWindow?.ShowLibraryPage();
        ActivateMainWindow();
    }

    // ---------------------------------------------------------------- tool windows

    public void OpenSettingsWindow()
    {
        if (TryShowCached(typeof(AppSettingsWindow))) return;
        var viewModel = new AppSettingsViewModel(AppSettings, MainWindowViewModel ?? _services.GetRequiredService<MainWindowViewModel>());
        Show(new AppSettingsWindow(viewModel), centerOn: MainWindow);
    }

    public void OpenDownloadListWindow()
    {
        if (TryShowCached(typeof(DownloadItemListWindow))) return;
        var viewModel = _services.GetRequiredService<DownloadItemListViewModel>();
        Show(new DownloadItemListWindow(viewModel), centerOn: MainWindow);
    }

    public void OpenAboutWindow()
    {
        if (TryShowCached(typeof(AboutWindow))) return;
        Show(new AboutWindow(), centerOn: MainWindow);
    }

    public void OpenProblemListWindow()
    {
        if (TryShowCached(typeof(AddonProblemListWindow))) return;
        if (MainWindowViewModel?.AddonRoot is not { } addonRoot) return;
        Show(new AddonProblemListWindow(new AddonProblemListViewModel(addonRoot)), centerOn: MainWindow);
    }

    public void OpenTagManagerWindow()
    {
        if (TryShowCached(typeof(AddonTagManagerWindow))) return;
        if (MainWindowViewModel is not { } mainViewModel) return;
        Show(new AddonTagManagerWindow(new AddonTagManagerViewModel(mainViewModel)), centerOn: MainWindow);
    }

    public void OpenVpkConflictListWindow()
    {
        if (TryShowCached(typeof(VpkAddonConflictListWindow))) return;
        if (MainWindowViewModel?.AddonRoot is not { } addonRoot) return;
        Show(new VpkAddonConflictListWindow(new VpkAddonConflictListViewModel(addonRoot)), centerOn: MainWindow);
    }

    public void OpenReplacementOverviewWindow()
    {
        if (TryShowCached(typeof(ReplacementOverviewWindow))) return;
        if (MainWindowViewModel?.AddonRoot is not { } addonRoot) return;
        var viewModel = new ReplacementOverviewViewModel(addonRoot, addon =>
            WeakReferenceMessenger.Default.Send(new AddonNodeJumpMessage(addon)));
        Show(new ReplacementOverviewWindow(viewModel), centerOn: MainWindow);
    }

    public void OpenWorkshopVpkFinderWindow()
    {
        if (TryShowCached(typeof(WorkshopVpkFinderWindow))) return;
        if (MainWindowViewModel is not { } mainViewModel) return;
        Show(new WorkshopVpkFinderWindow(new WorkshopVpkFinderViewModel(
            mainViewModel, _services.GetRequiredService<HttpClient>())), centerOn: MainWindow);
    }

    public void OpenFileCleanerWindow()
    {
        if (TryShowCached(typeof(FileCleanerWindow))) return;
        if (MainWindowViewModel?.AddonRoot is not { } addonRoot) return;
        Show(new FileCleanerWindow(new FileCleanerViewModel(addonRoot)), centerOn: MainWindow);
    }

    public void OpenAddonNameAutoSetterWindow()
    {
        if (TryShowCached(typeof(AddonNameAutoSetterWindow))) return;
        if (MainWindowViewModel?.AddonRoot is not { } addonRoot) return;
        Show(new AddonNameAutoSetterWindow(new AddonNameAutoSetterViewModel(addonRoot)), centerOn: MainWindow);
    }

    public void OpenFileNameFixerWindow()
    {
        if (TryShowCached(typeof(FileNameFixerWindow))) return;
        if (MainWindowViewModel?.AddonRoot is not { } addonRoot) return;
        Show(new FileNameFixerWindow(new FileNameFixerViewModel(addonRoot)), centerOn: MainWindow);
    }

    public void OpenNewWorkshopCollectionWindow(AddonRoot addonRoot, AddonGroup? addonGroup)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);
        var viewModel = new NewWorkshopCollectionViewModel(addonRoot, addonGroup);
        Show(new NewWorkshopCollectionWindow(viewModel), centerOn: MainWindow);
    }

    public async Task ShowImportResultAsync(AddonRoot.ImportResult importResult)
    {
        ArgumentNullException.ThrowIfNull(importResult);

        // The Avalonia version used ShowDialog (OS-modal). WinUI 3 has no built-in modal window; the window is
        // shown modeless but this method still completes only when it is closed, so the caller's flow matches.
        var window = new AddonImportResultWindow(new AddonImportResultViewModel(importResult));
        var closedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (sender, args) => closedTcs.TrySetResult();

        Show(window, centerOn: MainWindow);
        await closedTcs.Task;
    }

    public void ShowDeletionProgress(IEnumerable<(Task, string)> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var viewModel = new OperationsProgressViewModel();
        var window = new OperationsProgressWindow(viewModel)
        {
            Title = Texts.DeletingAddons,
        };
        window.OperationsProgressView.MessageTemplate = OperationsProgressViewMessageTemplates.Deletion;

        var closeHandler = new Action(window.Close);
        viewModel.CloseRequested += closeHandler;
        window.Closed += (sender, args) => viewModel.CloseRequested -= closeHandler;

        // Returns immediately: the window stays open while the operations keep running (as in the original).
        Show(window, centerOn: MainWindow);
        _ = new TaskOperationsProgressNotifier(operations, false, viewModel);
    }

    // ---------------------------------------------------------------- window plumbing

    /// <summary>Shows <paramref name="window"/>, applies the app theme and starts tracking it.</summary>
    private bool TryShowCached(Type type)
    {
        if (MainWindow?.FindCachedPage(type) is not { } page) return false;
        MainWindow.ShowHostedPage(page, cache: true);
        return true;
    }
    private void Show(HostedPage window, Window? centerOn = null)
    {
        if (MainWindow is null) throw new InvalidOperationException("Create MainWindow before navigating.");
        MainWindow.ShowHostedPage(window, cache: window is AppSettingsWindow or DownloadItemListWindow or AboutWindow or AddonProblemListWindow or AddonTagManagerWindow or VpkAddonConflictListWindow or ReplacementOverviewWindow or WorkshopVpkFinderWindow or FileCleanerWindow or AddonNameAutoSetterWindow or FileNameFixerWindow);
    }
    /// <summary>Registers the window with the theme service / active-window tracking and sets its icon.</summary>
    private void Attach(Window window)
    {
        _themeService.RegisterWindow(window);
        _openWindows.Add(new WeakReference<Window>(window));
        window.Activated += OnWindowActivated;
        window.Closed += OnWindowClosed;

        try
        {
            if (File.Exists(_iconPath))
            {
                window.AppWindow.SetIcon(_iconPath);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during setting the window icon.");
        }
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (sender is Window window && args.WindowActivationState != WindowActivationState.Deactivated)
        {
            _activeWindow = window;
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (sender is not Window window)
        {
            return;
        }

        window.Activated -= OnWindowActivated;
        window.Closed -= OnWindowClosed;
        _openWindows.RemoveAll(reference => !reference.TryGetTarget(out var target) || ReferenceEquals(target, window));

        if (ReferenceEquals(_activeWindow, window))
        {
            _activeWindow = null;
        }
    }

    /// <summary>Centers <paramref name="window"/> on <paramref name="owner"/> (or on the screen when there is none).</summary>
    private static void CenterOn(Window window, Window? owner)
    {
        try
        {
            var windowSize = window.AppWindow.Size;

            if (owner?.AppWindow is { } ownerAppWindow)
            {
                var ownerPosition = ownerAppWindow.Position;
                var ownerSize = ownerAppWindow.Size;
                window.AppWindow.Move(new Windows.Graphics.PointInt32(
                    ownerPosition.X + ((ownerSize.Width - windowSize.Width) / 2),
                    ownerPosition.Y + ((ownerSize.Height - windowSize.Height) / 2)));
                return;
            }

            if (DisplayArea.GetFromWindowId(window.AppWindow.Id, DisplayAreaFallback.Primary) is { } displayArea)
            {
                var workArea = displayArea.WorkArea;
                window.AppWindow.Move(new Windows.Graphics.PointInt32(
                    workArea.X + ((workArea.Width - windowSize.Width) / 2),
                    workArea.Y + ((workArea.Height - windowSize.Height) / 2)));
            }
        }
        catch (Exception ex)
        {
            // Centering is cosmetic; never let it break opening a window.
            Log.Error(ex, "Exception occurred during WindowService.CenterOn.");
        }
    }

    /// <summary>Closes every window this service opened (used by the shutdown sequence).</summary>
    public void CloseAllWindows()
    {
        MainWindow?.ClearHostedPages(force: true);
        foreach (var reference in _openWindows.ToArray())
        {
            if (reference.TryGetTarget(out var window))
            {
                try
                {
                    window.Close();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception occurred during closing a window.");
                }
            }
        }
        _openWindows.Clear();
    }

    // ---------------------------------------------------------------- P3 additions (still P4/P5 work)

    public void OpenAddonTagEditorWindow(AddonNode addon)
    {
        ArgumentNullException.ThrowIfNull(addon);
        Show(new AddonTagEditorWindow(new AddonTagEditorViewModel(addon)), centerOn: MainWindow);
    }

    public void OpenAddonNodeCustomizeImageWindow(AddonNode addon)
    {
        ArgumentNullException.ThrowIfNull(addon);
        Show(new AddonNodeCustomizeImageWindow(new AddonNodeCustomizeImageViewModel(addon)), centerOn: MainWindow);
    }

    public Task<IReadOnlyList<AddonNode>?> PickAddonNodesAsync(AddonRoot addonRoot, bool allowMultiple, string title)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);
        var viewModel = new AddonNodePickerViewModel(addonRoot) { AllowMultiple = allowMultiple };
        var window = new AddonNodePickerWindow(viewModel, title);
        var result = new TaskCompletionSource<IReadOnlyList<AddonNode>?>(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => result.TrySetResult(window.SelectedAddons);
        Show(window, centerOn: MainWindow);
        return result.Task;
    }

    /// <summary>
    /// Temporary scaffolding for the windows that P4/P5 still have to port: logs the request and tells the
    /// caller which stage will provide it, instead of silently doing nothing.
    /// </summary>
    private void OpenNotPortedYet(string methodName, string windowName, string phase)
    {
        Log.Warning("WindowService.{Method} was called but {Window} is not ported yet ({Phase}).", methodName, windowName, phase);
        _ = ShowNotPortedDialogAsync(windowName, phase);
    }

    private async Task ShowNotPortedDialogAsync(string windowName, string phase)
    {
        try
        {
            var dialogs = _services.GetRequiredService<IDialogService>();
            await dialogs.ShowInfoAsync(
                $"{windowName} has not been ported yet ({phase}).",
                "FireAxeWinUI3");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during showing the not-ported-yet dialog.");
        }
    }
}
