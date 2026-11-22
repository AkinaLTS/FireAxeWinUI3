using System;
using System.Collections.Specialized;
using FireAxe.ViewModels;
using FireAxe.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FireAxe.Views;

public sealed partial class AddonTagManagerWindow : HostedPage
{
    public AddonTagManagerWindow(AddonTagManagerViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        InitializeComponent();
        ((FrameworkElement)Content).DataContext = viewModel;
        _ = new ViewModelLifecycleBinder((FrameworkElement)Content);
        tagsList.SelectionChanged += OnSelectionChanged;
        ViewModel.SelectedTags.CollectionChanged += OnSelectedTagsChanged;
        Closed += OnClosed;
    }

    public AddonTagManagerViewModel ViewModel { get; }

    private bool _isSyncingSelection;

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_isSyncingSelection)
        {
            return;
        }
        _isSyncingSelection = true;
        try
        {
            ViewModel.SelectedTags.Clear();
            foreach (var item in tagsList.SelectedItems)
            {
                if (item is string tag)
                {
                    ViewModel.SelectedTags.Add(tag);
                }
            }
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    private void OnSelectedTagsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (_isSyncingSelection)
        {
            return;
        }
        _isSyncingSelection = true;
        tagsList.SelectionChanged -= OnSelectionChanged;
        try
        {
            tagsList.SelectedItems.Clear();
            foreach (var tag in ViewModel.SelectedTags)
            {
                tagsList.SelectedItems.Add(tag);
            }
        }
        finally
        {
            tagsList.SelectionChanged += OnSelectionChanged;
            _isSyncingSelection = false;
        }
    }

    private void OnClosed(object? sender, EventArgs args)
    {
        tagsList.SelectionChanged -= OnSelectionChanged;
        ViewModel.SelectedTags.CollectionChanged -= OnSelectedTagsChanged;
        ViewModel.Deactivate();
    }
}
