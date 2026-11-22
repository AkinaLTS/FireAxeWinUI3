// Ported from FireAxe_Alt\FireAxe.GUI\Views\ReplacementOverviewWindow.axaml.cs.
using System;
using FireAxe.ViewModels;
using FireAxe.Views.Controls;
using Microsoft.UI.Xaml;

namespace FireAxe.Views;

public sealed partial class ReplacementOverviewWindow : HostedPage
{
    public ReplacementOverviewWindow(ReplacementOverviewViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        InitializeComponent();
        ((FrameworkElement)Content).DataContext = viewModel;
        _ = new ViewModelLifecycleBinder((FrameworkElement)Content);
        Closed += OnClosed;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        if (!viewModel.IsValid) Close();
    }

    public ReplacementOverviewViewModel ViewModel { get; }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.IsValid) && !ViewModel.IsValid) Close();
    }

    private void OnClosed(object? sender, EventArgs args)
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.Deactivate();
        ViewModel.Dispose();
    }
}
