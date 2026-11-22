// Ported from FireAxe_Alt\FireAxe.GUI\Views\VpkAddonSectionView.axaml.cs (the Alt file only called
// InitializeComponent; the view has no behaviour of its own).
// The lifecycle binder is deliberately not created here: VpkAddonViewModel is owned by AddonNodeView, whose
// own binder already activates it, and the nested VpkAddonConflictingDetailsView activates its own view
// model.
using Microsoft.UI.Xaml.Controls;

namespace FireAxe.Views;

public sealed partial class VpkAddonSectionView : UserControl
{
    public VpkAddonSectionView()
    {
        InitializeComponent();
    }
}
