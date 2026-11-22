// Ported from FireAxe_Alt\FireAxe.GUI\Views\RefAddonNodeSectionView.axaml.cs
// The Alt version opened AddonNodePickerWindow directly from the view. Pickers belong to P4, so the view
// asks IWindowService (which logs a warning and returns null until the window is ported, P2 rule 7).
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FireAxe.Resources;
using FireAxe.Services;
using FireAxe.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FireAxe.Views;

public sealed partial class RefAddonNodeSectionView : UserControl
{
    public RefAddonNodeSectionView()
    {
        InitializeComponent();

        _ = new Controls.ViewModelLifecycleBinder(this);
    }

    private async void OnSetSourceButtonClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not RefAddonNodeViewModel viewModel || viewModel.RefAddon is not { } addon)
        {
            return;
        }

        var windowService = AppServices.TryGetService<IWindowService>();
        if (windowService is null)
        {
            return;
        }

        var selected = await windowService.PickAddonNodesAsync(addon.Root, false, Texts.SetSource);
        if (selected is not IReadOnlyList<AddonNode> { Count: 1 } result)
        {
            return;
        }

        // The selection may have changed while the picker was open, so re-read the addon like the Alt code.
        var current = (DataContext as RefAddonNodeViewModel)?.RefAddon;
        if (current is null)
        {
            return;
        }

        current.SourceAddonId = result[0].Id;
    }
}
