// Ported from FireAxe_Alt\FireAxe.GUI\Views\WorkshopVpkAddonSectionView.axaml.cs
// The Alt code-behind only registered three ReactiveUI interactions (show exception, confirm/notify the
// redundant VPK report). P1 moved all three into WorkshopVpkAddonViewModel behind IDialogService, so nothing
// is registered here; the remaining work is the publish-file-id editor (the Alt XAML bound Value two-way,
// which WinUI cannot do because the setter throws) and the OpenWorkshopPage() method call.
using System;
using FireAxe.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FireAxe.Views;

public sealed partial class WorkshopVpkAddonSectionView : UserControl
{
    public WorkshopVpkAddonSectionView()
    {
        InitializeComponent();

        // WorkshopVpkAddonViewModel is activated by AddonNodeView's binder; the download item view below
        // activates its own view model.
    }

    private void OnOpenWorkshopPageClick(object sender, RoutedEventArgs e)
    {
        (DataContext as WorkshopVpkAddonViewModel)?.OpenWorkshopPage();
    }

    private void OnItemIdSubmitting(object? sender, Controls.EditableTextBlockSubmittingEventArgs e)
    {
        if (DataContext is not WorkshopVpkAddonViewModel viewModel)
        {
            e.Cancel = true;
            return;
        }

        // ItemId parses the input and throws on an invalid published file id; keep the editor open and show
        // the message instead of losing the typed text.
        try
        {
            viewModel.ItemId = e.NewValue;
        }
        catch (Exception ex)
        {
            e.ErrorMessage = ex.Message;
        }
    }
}
