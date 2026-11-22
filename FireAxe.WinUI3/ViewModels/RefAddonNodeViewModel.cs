// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\RefAddonNodeViewModel.cs
using System;

namespace FireAxe.ViewModels;

public class RefAddonNodeViewModel : AddonNodeViewModel
{
    private AddonNodeSimpleViewModel? _sourceViewModel = null;

    public RefAddonNodeViewModel(RefAddonNode addon) : base(addon)
    {
    }

    public override Type AddonType => typeof(RefAddonNode);

    public RefAddonNode? RefAddon => (RefAddonNode?)base.Addon;

    public AddonNodeSimpleViewModel? SourceViewModel
    {
        get => _sourceViewModel;
        private set => SetProperty(ref _sourceViewModel, value);
    }

    protected override void OnNewAddon(AddonNode addon0, AddonSubscriptions subscriptions)
    {
        base.OnNewAddon(addon0, subscriptions);

        var addon = (RefAddonNode)addon0;

        void UpdateSourceViewModel()
        {
            var sourceId = addon.SourceAddonId;
            SourceViewModel = sourceId == Guid.Empty ? null : new AddonNodeSimpleViewModel(addon.Root, sourceId);
        }

        UpdateSourceViewModel();
        subscriptions.Add(addon, nameof(RefAddonNode.SourceAddonId), UpdateSourceViewModel);
    }

    protected override void OnNullAddon()
    {
        base.OnNullAddon();

        SourceViewModel = null;
    }
}
