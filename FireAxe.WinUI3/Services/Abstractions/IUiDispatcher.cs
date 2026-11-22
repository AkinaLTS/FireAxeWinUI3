// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\*.cs (Avalonia Avalonia.Threading.Dispatcher usage)
using System;
using System.Threading.Tasks;

namespace FireAxe.Services;

/// <summary>
/// The priority at which work is scheduled onto the UI thread.
/// </summary>
public enum UiDispatchPriority
{
    /// <summary>Normal priority. Use for everything the user is waiting on.</summary>
    Normal,

    /// <summary>
    /// Low priority. Use for bulk work that must not starve input handling
    /// (e.g. streaming search results into the collection). Maps to
    /// <c>DispatcherQueuePriority.Low</c> on WinUI 3 (see PORTING_PLAN §4 R5).
    /// </summary>
    Low
}

/// <summary>
/// Abstraction over the UI thread and its timer. Views/ViewModels never touch
/// <c>Microsoft.UI.Dispatching.DispatcherQueue</c> directly.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>Whether the calling thread can touch UI objects directly.</summary>
    bool HasThreadAccess { get; }

    /// <summary>Queues <paramref name="action"/> on the UI thread without waiting for it.</summary>
    void Post(Action action, UiDispatchPriority priority = UiDispatchPriority.Normal);

    /// <summary>Runs <paramref name="action"/> on the UI thread and awaits it.</summary>
    Task InvokeAsync(Action action, UiDispatchPriority priority = UiDispatchPriority.Normal);

    /// <summary>Runs the async <paramref name="action"/> on the UI thread and awaits it.</summary>
    Task InvokeAsync(Func<Task> action, UiDispatchPriority priority = UiDispatchPriority.Normal);

    /// <summary>
    /// Starts a UI-thread timer. <paramref name="tick"/> returns whether the timer should keep running.
    /// Disposing the returned handle stops it.
    /// </summary>
    IDisposable StartTimer(TimeSpan interval, Func<bool> tick);
}
