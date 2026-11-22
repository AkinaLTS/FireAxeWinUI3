// Ported from FireAxe_Alt\FireAxe.GUI\AppMutex.cs (single instance guard)
// Avalonia: named Mutex   -> Microsoft.Windows.AppLifecycle.AppInstance.FindOrRegisterForKey + activation redirect.
//
// Why AppInstance instead of the mutex: the plan (§3 P2) asks for it, and it also gives the second launch a way
// to bring the running instance to the foreground (RedirectActivationToAsync) instead of just saying "already
// running". See docs/phase-notes/P2-shell.md §3 for the measured behaviour in unpackaged mode.
using System;
using System.Threading;
using Microsoft.Windows.AppLifecycle;
using Serilog;

namespace FireAxe;

/// <summary>Single-instance guard for the app.</summary>
internal static class SingleInstance
{
    private static string InstanceKey => StartupPaths.InstanceKey;

    private static AppInstance? s_instance = null;

    /// <summary>
    /// Decides whether this process owns the instance key. Returns false when another instance is already
    /// running — the activation has then been redirected to it and this process should exit.
    /// </summary>
    /// <remarks>
    /// <c>RedirectActivationToAsync</c> is called on a worker thread and waited for synchronously, as in the
    /// WinAppSDK sample: on the UI thread it would deadlock with the message pump that has not started yet.
    /// </remarks>
    public static bool TryAcquire()
    {
        var instance = AppInstance.FindOrRegisterForKey(InstanceKey);
        s_instance = instance;

        if (instance.IsCurrent)
        {
            return true;
        }

        try
        {
            var activationArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            RedirectActivationTo(activationArgs, instance);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during redirecting the activation to the running instance.");
        }
        return false;
    }

    /// <summary>Raised when another launch asks this instance to come to the front.</summary>
    public static event EventHandler? ActivationRedirectedToThisInstance = null;

    /// <summary>Subscribes this instance to redirected activations. Call once, after the App exists.</summary>
    public static void StartListeningForRedirection()
    {
        if (s_instance is not { } instance)
        {
            return;
        }

        instance.Activated += (sender, args) =>
        {
            try
            {
                ActivationRedirectedToThisInstance?.Invoke(null, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during handling a redirected activation.");
            }
        };
    }

    private static void RedirectActivationTo(AppActivationArguments activationArgs, AppInstance targetInstance)
    {
        using var redirectSemaphore = new Semaphore(0, 1);
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                targetInstance.RedirectActivationToAsync(activationArgs).AsTask().Wait();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred during RedirectActivationToAsync.");
            }
            finally
            {
                redirectSemaphore.Release();
            }
        });
        redirectSemaphore.WaitOne();
    }
}
