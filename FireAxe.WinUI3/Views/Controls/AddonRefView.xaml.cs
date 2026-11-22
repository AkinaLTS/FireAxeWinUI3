// Ported from FireAxe_Alt\FireAxe.GUI\Views\AddonRefView.axaml.cs
//   * MessageBus.Current.SendMessage(...) -> WeakReferenceMessenger.Default.Send(...) (P1 already routes
//     AddonNodeJumpMessage to MainWindowViewModel, which selects the node and activates the main window).
//   * The Alt version only jumped when the referenced addon actually exists, which is what the second
//     (resolved) state of the visual tree represents; the guard below keeps that guarantee.
//   * AddonNodeSimpleViewModel.Refresh() only runs while the view model is activated, and the views that
//     create reference view models (parent group row, reference source, dependency list, conflict lists) do
//     not activate them. Avalonia did that implicitly through ReactiveUserControl<T>; the
//     ViewModelLifecycleBinder below reproduces it.
using CommunityToolkit.Mvvm.Messaging;
using FireAxe.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FireAxe.Views.Controls;

/// <summary>Shows an addon reference: either a jump button or the "missing addon" placeholder.</summary>
public sealed partial class AddonRefView : UserControl
{
    /// <summary>
    /// Hides the enable/disable toggle. The Alt version hid it from the "moving addons" bar with a
    /// style selector so that a drag-under-way list cannot accidentally flip addon states.
    /// </summary>
    public static readonly DependencyProperty ShowEnableButtonProperty =
        DependencyProperty.Register(nameof(ShowEnableButton), typeof(bool), typeof(AddonRefView), new PropertyMetadata(true, OnShowEnableButtonChanged));

    public AddonRefView()
    {
        InitializeComponent();

        // Keeps the displayed AddonNodeSimpleViewModel activated while the control is loaded; the binder
        // survives through the view's own event handlers, so no field is needed.
        _ = new ViewModelLifecycleBinder(this);
    }

    public bool ShowEnableButton
    {
        get => (bool)GetValue(ShowEnableButtonProperty);
        set => SetValue(ShowEnableButtonProperty, value);
    }

    private static void OnShowEnableButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (AddonRefView)d;
        if (view.enableButton is null)
        {
            return;
        }

        view.enableButton.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnJumpButtonClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AddonNodeSimpleViewModel viewModel)
        {
            return;
        }

        if (viewModel.Addon is not { } addon)
        {
            return;
        }

        WeakReferenceMessenger.Default.Send(new AddonNodeJumpMessage(addon));
    }
}
