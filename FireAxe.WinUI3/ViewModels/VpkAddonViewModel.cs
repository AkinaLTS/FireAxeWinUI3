// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\VpkAddonViewModel.cs
using System;
using System.Threading;

namespace FireAxe.ViewModels;

public abstract class VpkAddonViewModel : AddonNodeViewModel
{
    private VpkAddonInfo? _info = null;

    private VpkAddonConflictingDetailsViewModel? _conflictingDetailsViewModel = null;

    public VpkAddonViewModel(VpkAddon addon) : base(addon)
    {
    }

    public VpkAddon? VpkAddon => (VpkAddon?)base.Addon;

    public override Type AddonType => typeof(VpkAddon);

    public VpkAddonInfo? Info
    {
        get => _info;
        private set => SetProperty(ref _info, value);
    }

    public VpkAddonConflictingDetailsViewModel? ConflictingDetailsViewModel
    {
        get => _conflictingDetailsViewModel;
        private set => SetProperty(ref _conflictingDetailsViewModel, value);
    }

    protected override void OnNewAddon(AddonNode addon, AddonSubscriptions subscriptions)
    {
        base.OnNewAddon(addon, subscriptions);

        ConflictingDetailsViewModel = new VpkAddonConflictingDetailsViewModel((VpkAddon)addon);
    }

    protected override void OnNullAddon()
    {
        base.OnNullAddon();

        ConflictingDetailsViewModel = null;
        Info = null;
    }

    protected override void OnRefresh(CancellationToken cancellationToken)
    {
        base.OnRefresh(cancellationToken);

        var addon = VpkAddon;

        Info = addon?.RetrieveInfo();
    }

    protected override void OnClearCaches()
    {
        base.OnClearCaches();

        Info = null;
    }
}
