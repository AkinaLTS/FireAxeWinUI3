using System;
using System.Collections.Generic;
using FireAxe.ViewModels;
using FireAxe.Views.Controls;
using Microsoft.UI.Xaml;

namespace FireAxe.Views;

public sealed partial class AddonNodePickerWindow : HostedPage
{
    private IReadOnlyList<AddonNode>? _selectedAddons;

    public AddonNodePickerWindow(AddonNodePickerViewModel viewModel, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        InitializeComponent();
        ((FrameworkElement)Content).DataContext = viewModel;
        explorerView.DataContext = viewModel.ExplorerViewModel;
        _ = new ViewModelLifecycleBinder((FrameworkElement)Content);
        if (title is not null)
        {
            Title = title;
        }
        viewModel.CloseRequested += OnCloseRequested;
        Closed += OnClosed;
    }

    public AddonNodePickerViewModel ViewModel { get; }

    public bool IsSingleSelectionEnabled => !ViewModel.AllowMultiple;

    public IReadOnlyList<AddonNode>? SelectedAddons => _selectedAddons;

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        _selectedAddons = null;
        Close();
    }

    private void OnCloseRequested()
    {
        _selectedAddons = ViewModel.GetSelectedAddons();
        Close();
    }

    private void OnClosed(object? sender, EventArgs args)
    {
        ViewModel.CloseRequested -= OnCloseRequested;
        ViewModel.Deactivate();
        ViewModel.Dispose();
    }
}
