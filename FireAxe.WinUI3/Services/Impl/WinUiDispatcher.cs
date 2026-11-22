// Ported from FireAxe_Alt\FireAxe.GUI\ (Avalonia's Avalonia.Threading.Dispatcher + DispatcherTimer usages
// in ViewModels\; see ViewModels\MainWindowViewModel.cs, Services\DebounceAction.cs)
// Avalonia.Threading.Dispatcher        -> Microsoft.UI.Dispatching.DispatcherQueue
// DispatcherTimer.Run(interval, tick)   -> DispatcherQueue.CreateTimer()
using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;

namespace FireAxe.Services;

/// <summary>
/// <see cref="IUiDispatcher"/> over the WinUI 3 <see cref="DispatcherQueue"/>.
/// </summary>
public sealed class WinUiDispatcher : IUiDispatcher
{
    private readonly DispatcherQueue _queue;

    public WinUiDispatcher(DispatcherQueue queue)
    {
        ArgumentNullException.ThrowIfNull(queue);
        _queue = queue;
    }

    public bool HasThreadAccess => _queue.HasThreadAccess;

    public void Post(Action action, UiDispatchPriority priority = UiDispatchPriority.Normal)
    {
        ArgumentNullException.ThrowIfNull(action);
        _ = TryEnqueue(priority, () =>
        {
            action();
            return Task.CompletedTask;
        });
    }

    public Task InvokeAsync(Action action, UiDispatchPriority priority = UiDispatchPriority.Normal)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_queue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }
        return InvokeAsync(() =>
        {
            action();
            return Task.CompletedTask;
        }, priority);
    }

    public Task InvokeAsync(Func<Task> action, UiDispatchPriority priority = UiDispatchPriority.Normal)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_queue.HasThreadAccess)
        {
            return action();
        }
        return TryEnqueue(priority, action);
    }

    public IDisposable StartTimer(TimeSpan interval, Func<bool> tick)
    {
        ArgumentNullException.ThrowIfNull(tick);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);

        var timer = _queue.CreateTimer();
        timer.Interval = interval;
        timer.IsRepeating = true;

        void OnTick(DispatcherQueueTimer sender, object args)
        {
            bool keepRunning;
            try
            {
                keepRunning = tick();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Exception occurred during an IUiDispatcher timer tick.");
                sender.Stop();
                return;
            }

            if (!keepRunning)
            {
                sender.Stop();
            }
        }

        timer.Tick += OnTick;
        timer.Start();
        return new TimerHandle(timer, OnTick);
    }

    private Task TryEnqueue(UiDispatchPriority priority, Func<Task> body)
    {
        // RunContinuationsAsynchronously: the awaiting code must not run inline on the UI thread
        // (it often awaits the dialog/picker that is itself driven by the UI thread).
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool enqueued = _queue.TryEnqueue(ToDispatcherQueuePriority(priority), async () =>
        {
            try
            {
                await body();
                tcs.TrySetResult();
            }
            catch (OperationCanceledException ex)
            {
                tcs.TrySetCanceled(ex.CancellationToken);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        if (!enqueued)
        {
            tcs.TrySetException(new InvalidOperationException("The UI dispatcher queue has been shut down."));
        }
        return tcs.Task;
    }

    private static DispatcherQueuePriority ToDispatcherQueuePriority(UiDispatchPriority priority)
    {
        return priority switch
        {
            UiDispatchPriority.Low => DispatcherQueuePriority.Low,
            _ => DispatcherQueuePriority.Normal,
        };
    }

    private sealed class TimerHandle : IDisposable
    {
        private DispatcherQueueTimer? _timer;
        private Windows.Foundation.TypedEventHandler<DispatcherQueueTimer, object>? _handler;

        internal TimerHandle(DispatcherQueueTimer timer, Windows.Foundation.TypedEventHandler<DispatcherQueueTimer, object> handler)
        {
            _timer = timer;
            _handler = handler;
        }

        public void Dispose()
        {
            if (_timer is { } timer)
            {
                timer.Stop();
                if (_handler is { } handler)
                {
                    timer.Tick -= handler;
                }
                _timer = null;
                _handler = null;
            }
        }
    }
}
