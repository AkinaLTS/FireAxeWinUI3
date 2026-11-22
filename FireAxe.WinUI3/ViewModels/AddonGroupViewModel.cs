using System;

namespace FireAxe.ViewModels;

public class AddonGroupViewModel : AddonNodeViewModel
{
    public AddonGroupViewModel(AddonGroup group) : base(group)
    {

    }

    // WinUI divergence from Alt: this used to be `public new AddonGroup? Addon => (AddonGroup?)base.Addon;`.
    // WinUI's reflection based {Binding} cannot resolve a property that a derived class hides with `new`
    // (two `Addon` properties with different types make the path lookup fail silently, taking every
    // `{Binding Addon.*}` in the detail pane with it). The typed accessor therefore has its own name.
    public AddonGroup? AddonGroup => (AddonGroup?)base.Addon;

    public override Type AddonType => typeof(AddonGroup);
}
