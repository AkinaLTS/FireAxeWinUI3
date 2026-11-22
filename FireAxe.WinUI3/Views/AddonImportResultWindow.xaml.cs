// Ported from FireAxe_Alt\FireAxe.GUI\Views\AddonImportResultWindow.axaml.cs
using System;
using FireAxe.ViewModels;
using Microsoft.UI.Xaml;

namespace FireAxe.Views;

public sealed partial class AddonImportResultWindow : HostedPage
{
    public AddonImportResultWindow(AddonImportResultViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        ViewModel = viewModel;
        InitializeComponent();
    }

    public AddonImportResultViewModel ViewModel { get; }
}
