// Ported from FireAxe_Alt\FireAxe.GUI\Program.cs
// Avalonia's generated entry point -> an explicit WinUI 3 Main (App.g.i.cs's Main is disabled via
// DISABLE_XAML_GENERATED_MAIN in the csproj, so the logger and the single-instance guard run first,
// exactly like the Avalonia version).
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Serilog;

namespace FireAxe;

internal static class Program
{
    // Initialization code. Don't use any WinUI/third-party APIs or any SynchronizationContext-reliant code
    // before Application.Start is called: things aren't initialized yet and stuff might break.
    [STAThread]
    private static int Main(string[] args)
    {
#if FIREAXE_MANUAL_WASDK_BOOTSTRAP
        bool bootstrapInitialized = false;
#endif
        try
        {
            _ = CultureManager.Instance;
            SetupLogger();
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            Log.Information("FireAxe Start (Version: {Version})", AppGlobal.VersionString);
            RegisterObjectExplanations();

#if FIREAXE_MANUAL_WASDK_BOOTSTRAP
            if (!TryInitializeWindowsAppSdkBootstrap(out int bootstrapHr))
            {
                Log.Fatal("Windows App SDK bootstrap failed with HRESULT 0x{HResult:X8}.", bootstrapHr);
                return bootstrapHr;
            }
            bootstrapInitialized = true;
#endif

            // Single instance (was AppMutex.TryEnter()). The loser redirects its activation and exits.
            if (!SingleInstance.TryAcquire())
            {
                Log.Information("Another FireAxe instance is already running; activation redirected to it.");
                return 0;
            }

            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(callbackParams =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Unhandled Exception");
            CrashReporter.Run(ex);
            throw;
        }
        finally
        {
            OnExit();
#if FIREAXE_MANUAL_WASDK_BOOTSTRAP
            if (bootstrapInitialized)
            {
                Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.Shutdown();
            }
#endif
        }
    }

#if FIREAXE_MANUAL_WASDK_BOOTSTRAP
    private static bool TryInitializeWindowsAppSdkBootstrap(out int hr)
    {
        uint majorMinorVersion = Microsoft.WindowsAppSDK.Release.MajorMinor;
        string versionTag = Microsoft.WindowsAppSDK.Release.VersionTag;
        var minVersion = new Microsoft.Windows.ApplicationModel.DynamicDependency.PackageVersion(
            Microsoft.WindowsAppSDK.Runtime.Version.UInt64);
        return Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.TryInitialize(
            majorMinorVersion,
            versionTag,
            minVersion,
            Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.InitializeOptions.OnNoMatch_ShowUI,
            out hr);
    }
#endif

    internal static void OnExit()
    {
        Log.CloseAndFlush();
    }

    private static void SetupLogger()
    {
        // The Avalonia app also had .WriteTo.Console(), which goes nowhere in a WinExe (no console attached);
        // dropped so that the app does not need the console sink package. Everything else is identical.
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(StartupPaths.LogPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
            .CreateLogger();
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception occurred");
    }

    private static void RegisterObjectExplanations()
    {
        var defaultManager = ObjectExplanationManager.Default;
        ExceptionExplanations.Register(defaultManager);
        AddonProblemExplanations.Register(defaultManager);
    }
}
