// Ported from FireAxe_Alt\FireAxe.GUI\Views\AddonNodeNavBarItemView.axaml.cs
using FireAxe.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FireAxe.Views;

public sealed partial class AddonNodeNavBarItemView : UserControl
{
    public AddonNodeNavBarItemView()
    {
        InitializeComponent();
    }

    private void OnGotoButtonClick(object sender, RoutedEventArgs e)
    {
        (DataContext as AddonNodeNavBarItemViewModel)?.Goto();
    }
}
