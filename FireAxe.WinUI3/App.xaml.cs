// Ported from FireAxe_Alt\FireAxe.GUI\App.axaml.cs
//   IClassicDesktopStyleApplicationLifetime + ShutdownMode.OnMainWindowClose -> WinUI window lifetime
//   desktop.ShutdownRequested (cancel + close windows + dispose services + shutdown)
//       -> MainWindow.AppWindow.Closing (cancel + close windows + dispose services + Exit)
//   AvaloniaXamlLoader.Load(this) -> InitializeComponent()
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using FireAxe.Services;
using FireAxe.ViewModels;
using FireAxe.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Serilog;

namespace FireAxe;

public partial class App : Application
{
    public const string DocumentDirectoryName = "FireAxe";

    /// <summary>How long the shutdown waits for the services to dispose before forcing the process out.</summary>
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(15);

    private static bool s_isShuttingDown = false;

    private IServiceProvider? _services = null;

    private MainWindow? _mainWindow = null;

    private bool _shutdownStarted = false;

    public App()
    {
        InitializeComponent();

        UnhandledException += OnUnhandledException;
    }

    /// <summary>
    /// True once the app has begun shutting down. Windows that refuse to close while work is running
    /// (e.g. <see cref="Views.OperationsProgressWindow"/>) check this so that shutdown still works.
    /// </summary>
    public static bool IsShuttingDown => s_isShuttingDown;

    public IServiceProvider Services => _services ?? throw new InvalidOperationException($"{nameof(Services)} is not set.");

    public string DocumentDirectoryPath { get; } = StartupPaths.DataDirectory;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Directory.CreateDirectory(DocumentDirectoryPath);

        var services = BuildServiceProvider(DocumentDirectoryPath);
        _services = services;

        // R13: the service locator must be filled before the first ViewModel is constructed, otherwise the
        // nested ViewModels that use `new` silently get null services (no dialogs, no timers).
        AppServices.Provider = services;

        // Constructing AppSettings loads settings.json and applies the saved language and colour theme.
        var appSettings = services.GetRequiredService<AppSettings>();

        var windowService = services.GetRequiredService<WindowService>();
        var mainWindowViewModel = services.GetRequiredService<MainWindowViewModel>();

        _mainWindow = windowService.CreateMainWindow(mainWindowViewModel);
        _mainWindow.AppWindow.Closing += OnMainWindowClosing;
        windowService.ActivateMainWindow();

        var saveManager = services.GetRequiredService<SaveManager>();
        saveManager.Register(mainWindowViewModel);
        saveManager.Register(appSettings);
        saveManager.Run();

        // Another launch asking this instance to come forward (replaces "the mutex said no").
        SingleInstance.ActivationRedirectedToThisInstance += OnActivationRedirectedToThisInstance;
        SingleInstance.StartListeningForRedirection();

        // The Avalonia version called this from WhenActivated; P1 moved the lifecycle to the View,
        // so MainWindow calls it once its content is loaded (see Views\MainWindow.xaml.cs).
    }

    private void OnActivationRedirectedToThisInstance(object? sender, EventArgs e)
    {
        try
        {
            _services?.GetRequiredService<WindowService>().ActivateMainWindow();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during activating the main window.");
        }
    }

    private IServiceProvider BuildServiceProvider(string documentDirectoryPath)
    {
        var services = new ServiceCollection();

        // --- platform services (the 6 abstractions P1 defined) ---
        services.AddSingleton<IUiDispatcher>(new WinUiDispatcher(DispatcherQueue.GetForCurrentThread()));
        services.AddSingleton<WinUiAppThemeService>();
        services.AddSingleton<IAppThemeService>(sp => sp.GetRequiredService<WinUiAppThemeService>());
        services.AddSingleton<WindowService>();
        services.AddSingleton<IActiveWindowProvider>(sp => sp.GetRequiredService<WindowService>());
        services.AddSingleton<IWindowService>(sp => sp.GetRequiredService<WindowService>());
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IPickerService, PickerService>();
        services.AddSingleton<IClipboardService, WinUiClipboardService>();
        services.AddSingleton<IImageLoader, ImageLoader>();

        // --- app services (same set as the Avalonia composition root) ---
        services.AddSingleton(sp => new AppSettings(documentDirectoryPath, sp.GetRequiredService<IAppThemeService>()));
        services.AddSingleton<SaveManager>();
        services.AddSingleton<DownloadItemListViewModel>();
        services.AddSingleton<IDownloadService>(sp =>
        {
            var appSettings = sp.GetRequiredService<AppSettings>();
            var downloadService = new DownloadService(appSettings.GetDownloadServiceSettings());
            appSettings.DownloadService = downloadService;
            return downloadService;
        });
        services.AddSingleton(sp =>
        {
            var appSettings = sp.GetRequiredService<AppSettings>();
            var socketsHttpHandler = new SocketsHttpHandler();
            if (appSettings.GetWebProxy() is { } webProxy)
            {
                socketsHttpHandler.UseProxy = true;
                socketsHttpHandler.Proxy = webProxy;
            }
            var httpClient = new HttpClient(socketsHttpHandler);
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(".NET", null));
            return httpClient;
        });
        services.AddSingleton<MainWindowViewModel>();

        return services.BuildServiceProvider();
    }

    private void OnMainWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_shutdownStarted)
        {
            // Second close request (the one Application.Exit() issues): let it through.
            return;
        }

        if (_services?.GetService<WindowService>() is { HasBlockingHostedPage: true } windowService)
        {
            args.Cancel = true;
            windowService.ActivateMainWindow();
            return;
        }

        args.Cancel = true;
        _shutdownStarted = true;
        s_isShuttingDown = true;

        _ = ShutdownAsync();
    }

    /// <summary>
    /// Closes the windows, disposes the services and exits — with a watchdog that forces the process out
    /// if disposal hangs (the Avalonia version used a 15 second background thread for the same reason).
    /// </summary>
    private async Task ShutdownAsync()
    {
        StartShutdownWatchdog();

        try
        {
            _services?.GetService<SaveManager>()?.SaveAll(true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during saving on shutdown.");
        }

        try
        {
            _services?.GetService<WindowService>()?.CloseAllWindows();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during closing the windows on shutdown.");
        }

        try
        {
            if (_services is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else
            {
                (_services as IDisposable)?.Dispose();
            }
            Log.Information("All services disposed successfully.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during disposing services.");
        }

        Log.CloseAndFlush();
        Exit();
    }

    private static void StartShutdownWatchdog()
    {
        var watchdog = new Thread(() =>
        {
            try
            {
                Thread.Sleep(ShutdownTimeout);
                Log.Warning("Application Shutdown Timeout: The application will be forcibly terminated.");
                Log.CloseAndFlush();
            }
            finally
            {
                Environment.Exit(-1);
            }
        })
        {
            IsBackground = true,
        };
        watchdog.Start();
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Unhandled Exception");

        // Match the Avalonia behaviour: report, then terminate. Continuing after an unhandled exception
        // would leave the addon root / download service in an unknown state.
        e.Handled = true;

        try
        {
            CrashReporter.Run(e.Exception);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during CrashReporter.Run.");
        }

        Log.CloseAndFlush();
        Environment.Exit(-1);
    }
}
