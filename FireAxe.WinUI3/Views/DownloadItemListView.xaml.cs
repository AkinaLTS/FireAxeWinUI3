using System;
using System.Collections.Specialized;
using FireAxe.ViewModels;
using FireAxe.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FireAxe.Views;

public sealed partial class DownloadItemListView : UserControl
{
    public DownloadItemListView(DownloadItemListViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        _ = new ViewModelLifecycleBinder(this);
        ((INotifyCollectionChanged)viewModel.DownloadItemViewModels).CollectionChanged += OnDownloadItemsChanged;
    }

    public DownloadItemListViewModel ViewModel { get; }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        ViewModel.Selection = downloadsList.SelectedItems
            .OfType<DownloadItemViewModel>()
            .ToArray();
    }

    private void OnDownloadItemsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (args.Action == NotifyCollectionChangedAction.Reset)
        {
            downloadsList.SelectedItems.Clear();
            ViewModel.Selection = null;
            return;
        }

        var selection = ViewModel.Selection;
        if (selection is null)
        {
            return;
        }

        downloadsList.SelectionChanged -= OnSelectionChanged;
        try
        {
            downloadsList.SelectedItems.Clear();
            foreach (var item in selection)
            {
                if (ViewModel.DownloadItemViewModels.Contains(item))
                {
                    downloadsList.SelectedItems.Add(item);
                }
            }
        }
        finally
        {
            downloadsList.SelectionChanged += OnSelectionChanged;
        }
    }
}
