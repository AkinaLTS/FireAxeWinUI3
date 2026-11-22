// Ported from FireAxe_Alt\FireAxe.GUI\Views\AddonGroupSectionView.axaml.cs
using Microsoft.UI.Xaml.Controls;

namespace FireAxe.Views;

/// <summary>Enable strategy of the selected group (DataContext is set by <see cref="AddonNodeView"/>).</summary>
public sealed partial class AddonGroupSectionView : UserControl
{
    public AddonGroupSectionView()
    {
        InitializeComponent();
    }
}
