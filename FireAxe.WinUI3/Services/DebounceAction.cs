// New for P1: replaces Rx `Throttle(...)` / `DispatcherTimer` debounce patterns used by the
// Avalonia ViewModels (AddonNodeSimpleViewModel, AddonNodeExplorerViewModel, AppSettings).
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FireAxe.Services;

/// <summary>
/// Debounces an action: each <see cref="Run"/> restarts the delay, so only the last call in a burst runs.
/// The action is executed on the UI thread when a dispatcher is supplied.
/// </summary>
internal sealed class DebounceAction : IDisposable
{
    private readonly TimeSpan _delay;
    private readonly IUiDispatcher? _dispatcher;
    private readonly object _lock = new();

    private CancellationTokenSource? _cts;
    private bool _disposed;

    public DebounceAction(TimeSpan delay, IUiDispatcher? dispatcher = null)
    {
        _delay = delay;
        _dispatcher = dispatcher;
    }

    /// <summary>Schedules <paramref name="action"/>, cancelling any pending run.</summary>
    public void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        CancellationTokenSource cts;
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            var previous = _cts;
            cts = new CancellationTokenSource();
            _cts = cts;

            if (previous is not null)
            {
                previous.Cancel();
                previous.Dispose();
            }
        }

        _ = RunAsync(action, cts);
    }

    /// <summary>Cancels any pending run without disposing the debouncer, so future <see cref="Run"/> calls still work.</summary>
    public void Cancel()
    {
        lock (_lock)
        {
            var cts = _cts;
            _cts = null;
            if (cts is not null)
            {
                cts.Cancel();
                cts.Dispose();
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            var cts = _cts;
            _cts = null;
            if (cts is not null)
            {
                cts.Cancel();
                cts.Dispose();
            }
        }
    }

    private async Task RunAsync(Action action, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(_delay, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        lock (_lock)
        {
            if (_disposed || !ReferenceEquals(_cts, cts))
            {
                return;
            }
            _cts = null;
            cts.Dispose();
        }

        if (_dispatcher is { } dispatcher)
        {
            dispatcher.Post(action);
        }
        else
        {
            action();
        }
    }
}
