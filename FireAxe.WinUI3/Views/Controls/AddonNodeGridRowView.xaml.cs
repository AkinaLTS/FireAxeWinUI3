// Ported from FireAxe_Alt\FireAxe.GUI\Views\AddonNodeGridRowView.axaml.cs
// The Avalonia base class (ReactiveUserControl<AddonNodeListItemViewModel>) became the shared binder, see
// AddonNodeListItemViewModelBinder.cs for why inheritance is not an option in WinUI 3.
using FireAxe.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FireAxe.Views.Controls;

/// <summary>One addon drawn as a fixed-height table row (the explorer's grid view).</summary>
public sealed partial class AddonNodeGridRowView : UserControl
{
    private readonly AddonNodeListItemViewModelBinder _binder;

    public AddonNodeGridRowView()
    {
        InitializeComponent();
        _binder = new AddonNodeListItemViewModelBinder(this, rootGrid, addon => new AddonNodeListItemViewModel(addon));
    }

    /// <summary>The item view model this row is currently showing.</summary>
    public AddonNodeListItemViewModel? ViewModel => _binder.ViewModel;
}
