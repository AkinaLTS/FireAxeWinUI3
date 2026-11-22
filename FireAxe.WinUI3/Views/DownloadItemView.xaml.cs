// Ported from FireAxe_Alt\FireAxe.GUI\Views\DownloadItemView.axaml.cs
// The Alt view activated its view model through ReactiveUserControl<T>; the download item view model owns a
// 250 ms refresh timer that only runs while activated, so the binder below is what keeps the progress
// numbers live.
using Microsoft.UI.Xaml.Controls;

namespace FireAxe.Views;

public sealed partial class DownloadItemView : UserControl
{
    public DownloadItemView()
    {
        InitializeComponent();

        _ = new Controls.ViewModelLifecycleBinder(this);
    }
}
