// Ported from FireAxe_Alt\FireAxe.GUI\Views\AddonNodeEnableButton.axaml.cs
//   * WhenAnyValue(ViewModel.EnableState) -> DataContextChanged + PropertyChanged on EnableState.
//   * app.FindResource("icon_*")            -> FireAxe.Views.Icons (WinUI cannot store path data as a
//                                             resource, see Icons.cs).
//   * DoubleTapped e.Handled = true         -> kept verbatim: it stops a double click on the button from
//                                             also opening the group in the explorer.
using System;
using System.ComponentModel;
using FireAxe.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace FireAxe.Views.Controls;

/// <summary>The round enable/disable toggle drawn in front of an addon.</summary>
public sealed partial class AddonNodeEnableButton : UserControl
{
    private AddonNodeSimpleViewModel? _viewModel = null;

    public AddonNodeEnableButton()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        DoubleTapped += OnDoubleTapped;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Bind(DataContext as AddonNodeSimpleViewModel);
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        Bind(args.NewValue as AddonNodeSimpleViewModel);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Recycled list containers re-use the control; releasing the handler keeps the addon alive-free.
        Bind(null);
    }

    private void Bind(AddonNodeSimpleViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            UpdateIcon();
            return;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateIcon();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(AddonNodeSimpleViewModel.EnableState))
        {
            UpdateIcon();
        }
    }

    private void UpdateIcon()
    {
        string data;
        Windows.UI.Color color;

        switch (_viewModel?.EnableState ?? AddonNodeEnableState.Disabled)
        {
            case AddonNodeEnableState.Enabled:
                data = Icons.Enabled;
                color = Microsoft.UI.Colors.Lime;
                break;
            case AddonNodeEnableState.EnabledSuppressed:
                data = Icons.EnabledSuppressed;
                color = Microsoft.UI.Colors.Orange;
                break;
            default:
                data = Icons.Disabled;
                color = Microsoft.UI.Colors.Gray;
                break;
        }

        icon.Data = data;
        icon.Foreground = new SolidColorBrush(color);
    }

    private void OnDoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        e.Handled = true;
    }
}
