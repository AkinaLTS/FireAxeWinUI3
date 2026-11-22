using System;
using FireAxe.ViewModels;
using FireAxe.Views.Controls;
using Microsoft.UI.Xaml;

namespace FireAxe.Views;

public sealed partial class FileNameFixerWindow : HostedPage
{
    public FileNameFixerWindow(FileNameFixerViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        InitializeComponent();
        ((FrameworkElement)Content).DataContext = viewModel;
        _ = new ViewModelLifecycleBinder((FrameworkElement)Content);
        Closed += OnClosed;
    }

    public FileNameFixerViewModel ViewModel { get; }

    private void OnClosed(object? sender, EventArgs args)
    {
        ViewModel.Deactivate();
    }
}
