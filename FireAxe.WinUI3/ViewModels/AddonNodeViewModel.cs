// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\AddonNodeViewModel.cs
using FireAxe.Resources;
using System;

namespace FireAxe.ViewModels;

public class AddonNodeViewModel : AddonNodeSimpleViewModel
{
    private AddonNodeDependenciesViewModel? _dependenciesViewModel = null;

    public AddonNodeViewModel(AddonNode addon) : base(addon)
    {
    }

    public string? AddonIdString
    {
        get => Addon?.Id.ToString();
        set
        {
            if (!Guid.TryParse(value, out var guid) || guid == Guid.Empty)
            {
                throw new ArgumentException(Texts.InvalidGuid);
            }

            var addon = Addon;
            if (addon is null)
            {
                return;
            }

            addon.Id = guid;
        }
    }

    public string? Priority
    {
        get => Addon?.Priority.ToString();
        set
        {
            if (!int.TryParse(value, out int priority))
            {
                throw new ArgumentException(Texts.ValueMustBeInteger);
            }

            var addon = Addon;
            if (addon == null)
            {
                return;
            }

            addon.Priority = priority;
        }
    }

    public AddonNodeDependenciesViewModel? DependenciesViewModel
    {
        get => _dependenciesViewModel;
        private set => SetProperty(ref _dependenciesViewModel, value);
    }

    public static AddonNodeViewModel Create(AddonNode addonNode)
    {
        ArgumentNullException.ThrowIfNull(addonNode);

        if (addonNode is AddonGroup group)
        {
            return new AddonGroupViewModel(group);
        }
        else if (addonNode is RefAddonNode refAddon)
        {
            return new RefAddonNodeViewModel(refAddon);
        }
        else if (addonNode is LocalVpkAddon localVpkAddon)
        {
            return new LocalVpkAddonViewModel(localVpkAddon);
        }
        else if (addonNode is WorkshopVpkAddon workshopVpkAddon)
        {
            return new WorkshopVpkAddonViewModel(workshopVpkAddon);
        }
        else
        {
            return new AddonNodeViewModel(addonNode);
        }
    }

    protected override void OnNewAddon(AddonNode addon, AddonSubscriptions subscriptions)
    {
        base.OnNewAddon(addon, subscriptions);

        DependenciesViewModel = new AddonNodeDependenciesViewModel(addon);

        subscriptions.Add(addon, nameof(AddonNode.Id), () => OnPropertyChanged(nameof(AddonIdString)));
        subscriptions.Add(addon, nameof(AddonNode.Priority), () => OnPropertyChanged(nameof(Priority)));
    }

    protected override void OnNullAddon()
    {
        base.OnNullAddon();

        DependenciesViewModel = null;

        OnPropertyChanged(nameof(AddonIdString));
        OnPropertyChanged(nameof(Priority));
    }
}
