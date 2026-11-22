// Ported from FireAxe_Alt\FireAxe.GUI\Views\VpkAddonConflictingDetailsView.axaml.cs
// The Alt code-behind registered ReactiveUI interaction handlers for the two "ignore/remove all"
// confirmations. P1 moved those confirmations into the view model (IDialogService), so nothing is left to
// register here; only the two per-file actions still need a Click handler because they are plain methods.
using FireAxe.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FireAxe.Views;

public sealed partial class VpkAddonConflictingDetailsView : UserControl
{
    public VpkAddonConflictingDetailsView()
    {
        InitializeComponent();

        // RebuildConflictingAddonWithFiles/RebuildConflictingFileWithAddons run from Activate().
        _ = new Controls.ViewModelLifecycleBinder(this);
    }

    private void OnIgnoreConflictingFileClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not VpkAddonConflictingDetailsViewModel viewModel)
        {
            return;
        }

        // The button's DataContext is the item view model, whose File is the path to ignore.
        if (sender is FrameworkElement { DataContext: ConflictingVpkFileWithAddonsViewModel item })
        {
            viewModel.IgnoreConflictingFile(item.File);
        }
    }

    private void OnRemoveConflictIgnoringFileClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not VpkAddonConflictingDetailsViewModel viewModel)
        {
            return;
        }

        // The item is the file path itself.
        if (sender is FrameworkElement { DataContext: string file })
        {
            viewModel.RemoveConflictIgnoringFile(file);
        }
    }
}
