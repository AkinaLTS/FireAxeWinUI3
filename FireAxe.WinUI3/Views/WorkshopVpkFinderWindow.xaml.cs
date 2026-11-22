// Ported from FireAxe_Alt\FireAxe.GUI\Views\WorkshopVpkFinderWindow.axaml.cs.
using System;
using FireAxe.ViewModels;
using FireAxe.Views.Controls;
using Microsoft.UI.Xaml;

namespace FireAxe.Views;

public sealed partial class WorkshopVpkFinderWindow : HostedPage
{
    private readonly Action _closeRequestedHandler;

    public WorkshopVpkFinderWindow(WorkshopVpkFinderViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        InitializeComponent();
        ((FrameworkElement)Content).DataContext = viewModel;
        _ = new ViewModelLifecycleBinder((FrameworkElement)Content);
        _closeRequestedHandler = Close;
        viewModel.CloseRequested += _closeRequestedHandler;
        Closed += OnClosed;
    }

    public WorkshopVpkFinderViewModel ViewModel { get; }

    private void OnClosed(object? sender, EventArgs args)
    {
        ViewModel.CloseRequested -= _closeRequestedHandler;
        ViewModel.Deactivate();
        ViewModel.Dispose();
    }
}
