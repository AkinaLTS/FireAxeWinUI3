using System;
using FireAxe.ViewModels;
using FireAxe.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FireAxe.Views;

public sealed partial class VpkAddonConflictListWindow : HostedPage
{
    public VpkAddonConflictListWindow(VpkAddonConflictListViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        InitializeComponent();
        ((FrameworkElement)Content).DataContext = viewModel;
        _ = new ViewModelLifecycleBinder((FrameworkElement)Content);
        Closed += OnClosed;
        conflictTabs.SelectionChanged += OnTabSelectionChanged;
        conflictTabs.SelectedIndex = 0;
    }

    public VpkAddonConflictListViewModel ViewModel { get; }

    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        bool showAddons = conflictTabs.SelectedIndex == 0;
        addonsPanel.Visibility = showAddons ? Visibility.Visible : Visibility.Collapsed;
        filesPanel.Visibility = showAddons ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnClosed(object? sender, EventArgs args)
    {
        conflictTabs.SelectionChanged -= OnTabSelectionChanged;
        ViewModel.Deactivate();
        ViewModel.Dispose();
    }
}
