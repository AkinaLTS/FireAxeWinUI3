using System;
using FireAxe.ViewModels;
using FireAxe.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FireAxe.Views;

public sealed partial class DownloadItemListWindow : HostedPage
{
    private readonly DownloadItemListView _downloadListView;

    public DownloadItemListWindow(DownloadItemListViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        _downloadListView = new DownloadItemListView(viewModel);
        downloadsHost.Child = _downloadListView;
        _ = new ViewModelLifecycleBinder((FrameworkElement)Content);
        Closed += OnClosed;
    }

    private void OnClosed(object? sender, EventArgs args)
    {
        _downloadListView.ViewModel.Deactivate();
    }
}
