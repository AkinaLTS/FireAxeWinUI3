using System;
using FireAxe.ViewModels;
using FireAxe.Views.Controls;
using Microsoft.UI.Xaml;

namespace FireAxe.Views;

public sealed partial class NewWorkshopCollectionWindow : HostedPage
{
    public NewWorkshopCollectionWindow(NewWorkshopCollectionViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        InitializeComponent();
        ((FrameworkElement)Content).DataContext = viewModel;
        _ = new ViewModelLifecycleBinder((FrameworkElement)Content);
        viewModel.CloseRequested += OnCloseRequested;
        Closed += OnClosed;
    }

    public NewWorkshopCollectionViewModel ViewModel { get; }

    private void OnCloseRequested() => Close();

    private void OnClosed(object? sender, EventArgs args)
    {
        ViewModel.CloseRequested -= OnCloseRequested;
        ViewModel.Deactivate();
        ViewModel.Dispose();
    }
}
