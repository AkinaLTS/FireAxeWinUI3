// Replacement for FireAxe_Alt\FireAxe.GUI\Views\AddonNodeListItemView.cs
// (a ReactiveUserControl<AddonNodeListItemViewModel> base class shared by the grid-row and tile views).
//
// WinUI 3 cannot do this with inheritance: the root element of a XAML file has to be the x:Class type
// itself, so a view whose code-behind derives from a *custom* UserControl cannot have its own XAML.
// The behaviour is therefore composed instead of inherited, and both item views use this binder:
//   * Avalonia: DataContext="{Binding, Converter=AddonNodeConverters.ToListItemViewModel}"
//                + WhenActivated(...) driving the VM lifecycle.
//   * Here:     the list item keeps the AddonNode as its DataContext. The binder watches that, creates a
//               new AddonNodeListItemViewModel (one per addon, exactly like the converter did) and hands it
//               to the *inner* root panel, whose subtree is what the {Binding}s in the XAML talk to.
//               Only the inner panel is retargeted, so the view itself keeps inheriting its DataContext from
//               the recycled ListViewItem -- that is what makes DataContextChanged fire again for the next
//               item (a local DataContext on the view root would cut the inheritance and freeze the first
//               addon).
using System;
using FireAxe.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FireAxe.Views.Controls;

/// <summary>
/// Turns the <see cref="AddonNode"/> DataContext of a list-item view into an
/// <see cref="AddonNodeListItemViewModel"/> and keeps its lifecycle in step with the control.
/// </summary>
internal sealed class AddonNodeListItemViewModelBinder
{
    private readonly UserControl _view;
    private readonly FrameworkElement _contentRoot;
    private readonly Func<AddonNode, AddonNodeListItemViewModel> _factory;

    private AddonNodeListItemViewModel? _viewModel;
    private AddonNode? _addon;

    internal AddonNodeListItemViewModelBinder(
        UserControl view,
        FrameworkElement contentRoot,
        Func<AddonNode, AddonNodeListItemViewModel> factory)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _contentRoot = contentRoot ?? throw new ArgumentNullException(nameof(contentRoot));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));

        _view.DataContextChanged += OnDataContextChanged;
        _view.Loaded += OnLoaded;
        _view.Unloaded += OnUnloaded;
    }

    /// <summary>The view model the view's own bindings talk to (null until the first item arrives).</summary>
    internal AddonNodeListItemViewModel? ViewModel => _viewModel;

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (args.NewValue is not AddonNode addon || ReferenceEquals(addon, _addon))
        {
            return;
        }

        if (_viewModel is not null)
        {
            _viewModel.Deactivate();
        }

        _addon = addon;
        _viewModel = _factory(addon);
        _contentRoot.DataContext = _viewModel;

        if (_view.IsLoaded)
        {
            _viewModel.Activate();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel?.Activate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Containers are recycled, so this can run while the view is still on screen.
        _viewModel?.Deactivate();
    }
}
