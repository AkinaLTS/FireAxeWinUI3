using System;
using FireAxe.ViewModels;
using FireAxe.Views.Controls;
using Microsoft.UI.Xaml;

namespace FireAxe.Views;

public sealed partial class AddonNodeCustomizeImageWindow : HostedPage
{
    public AddonNodeCustomizeImageWindow(AddonNodeCustomizeImageViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        InitializeComponent();
        ((FrameworkElement)Content).DataContext = viewModel;
        _ = new ViewModelLifecycleBinder((FrameworkElement)Content);
        Closed += OnClosed;
    }

    public AddonNodeCustomizeImageViewModel ViewModel { get; }

    private void OnClosed(object? sender, EventArgs args)
    {
        ViewModel.Deactivate();
    }
}
