using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FireAxe;

public abstract class ObservableObject : INotifyPropertyChanged
{
    /// <summary>
    /// The context that created this object, used to marshal notifications back to it.
    /// </summary>
    private readonly SynchronizationContext? synchronizationContext = SynchronizationContext.Current;

    public event PropertyChangedEventHandler? PropertyChanged = null;

    protected bool NotifyAndSetIfChanged<T>(ref T field, in T value, [CallerMemberName] string? propertyName = null)
    {
        ArgumentNullException.ThrowIfNull(propertyName);

        if (EqualityComparer<T>.Default.Equals(value, field))
        {
            return false;
        }

        field = value;
        NotifyChanged(propertyName);
        return true;
    }

    protected void NotifyChanged([CallerMemberName] string? propertyName = null)
    {
        ArgumentNullException.ThrowIfNull(propertyName);

        var handler = PropertyChanged;

        if (handler is null)
        {
            return;
        }

        // WinUI delivers PropertyChanged through a WinRT delegate that rejects a call from a thread other
        // than the one that owns the delegate (COMException 0x8001010E "The application called an interface
        // that was marshalled for a different thread"). Avalonia tolerated cross-thread notifications, so
        // the ported models kept raising them from thread pool continuations. Marshal them back to the
        // context that created the model; when the model was created off the UI thread there is nothing to
        // marshal to and the previous behaviour is kept.
        var context = synchronizationContext;

        if (context is null || ReferenceEquals(SynchronizationContext.Current, context))
        {
            handler(this, new PropertyChangedEventArgs(propertyName));
            return;
        }

        context.Post(
            static state =>
            {
                var (target, args) = ((ObservableObject Target, PropertyChangedEventArgs Args))state!;
                target.PropertyChanged?.Invoke(target, args);
            },
            (this, new PropertyChangedEventArgs(propertyName)));
    }
}
