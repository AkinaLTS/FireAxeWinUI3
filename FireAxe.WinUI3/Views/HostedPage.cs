using System;
using Microsoft.UI.Xaml.Controls;

namespace FireAxe.Views;

/// <summary>A real content control hosted by MainWindow; Close ends its navigation session.</summary>
public class HostedPage : UserControl
{
    public HostedPage()
    {
        HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch;
        VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch;
        HorizontalContentAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch;
        VerticalContentAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch;
    }

    public string Title { get; set; } = string.Empty;
    public event EventHandler? Closed;
    internal event Action<HostedPage>? CloseRequested;
    internal bool IsClosed { get; private set; }
    internal bool PendingClose { get; private set; }
    public virtual bool CanClose => true;

    public void Close()
    {
        if (!CanClose) return;
        if (CloseRequested is null) PendingClose = true;
        else CloseRequested.Invoke(this);
    }

    internal void CompleteClose()
    {
        if (IsClosed) return;
        IsClosed = true;
        Closed?.Invoke(this, EventArgs.Empty);
    }
}
