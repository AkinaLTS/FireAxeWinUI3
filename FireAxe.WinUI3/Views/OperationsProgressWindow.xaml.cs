// Ported from FireAxe_Alt\FireAxe.GUI\Views\OperationsProgressWindow.axaml.cs
//   Closing (WindowClosingEventArgs) -> AppWindow.Closing (AppWindowClosingEventArgs)
//   ReactiveWindow<T> DataContext     -> ViewModel property assigned in the constructor
using System;
using FireAxe.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace FireAxe.Views;

public sealed partial class OperationsProgressWindow : HostedPage
{
    public OperationsProgressWindow(OperationsProgressViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        ViewModel = viewModel;
        InitializeComponent();

        operationsProgressView.ViewModel = viewModel;

    }

    public OperationsProgressViewModel ViewModel { get; }

    public OperationsProgressView OperationsProgressView => operationsProgressView;

    public override bool CanClose => ViewModel.IsDone || App.IsShuttingDown;
}