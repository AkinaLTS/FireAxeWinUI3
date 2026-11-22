// Ported from FireAxe_Alt\FireAxe.GUI\Views\AddonNodeSectionViewDecorator.axaml.cs
using Microsoft.UI.Xaml.Controls;

namespace FireAxe.Views;

/// <summary>Wraps one section view in the shared level-1 border.</summary>
public sealed partial class AddonNodeSectionViewDecorator : UserControl
{
    public AddonNodeSectionViewDecorator(Control content)
    {
        InitializeComponent();

        decorator.Child = content;
    }
}
