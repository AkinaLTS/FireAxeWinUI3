// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\ReplacementOverviewViewModel.cs
// ReactiveUI (ViewModelBase/ReactiveCommand/IActivatableViewModel/WhenActivated/ViewModelActivator) ->
//   FireAxe.ViewModels.ViewModelBase + [ObservableProperty] / [RelayCommand] + IViewModelLifecycle.
// Interaction<Exception, Unit> ShowExceptionInteraction -> IDialogService.ShowExceptionAsync.
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FireAxe.Resources;
using FireAxe.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FireAxe.ViewModels;

public enum ReplacementOverviewFilter
{
    All,
    Replaced,
    Vanilla,
    Conflict,
    InstalledButDisabled
}

public sealed class ReplacementOverviewFilterOption
{
    public ReplacementOverviewFilterOption(string name, ReplacementOverviewFilter filter)
    {
        ArgumentNullException.ThrowIfNull(name);

        Name = name;
        Filter = filter;
    }

    public string Name { get; }

    public ReplacementOverviewFilter Filter { get; }

    public override string ToString() => Name;
}

public sealed class ReplacementCategoryGroupViewModel
{
    public ReplacementCategoryGroupViewModel(string title, IReadOnlyList<ReplacementSlotItemViewModel> items)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(items);

        Title = title;
        Items = items;
    }

    public string Title { get; }

    public IReadOnlyList<ReplacementSlotItemViewModel> Items { get; }
}

public sealed class ReplacementSlotItemViewModel : ViewModelBase
{
    public ReplacementSlotItemViewModel(ReplacementSlotStatus status, Action<VpkAddon> locate)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(locate);

        Slot = status.Slot;
        State = status.State;
        StateText = GetStateText(status.State);
        SecondaryName = Slot.IsChineseCulture ? Slot.DisplayNameEn : Slot.DisplayNameZh;
        Addons = status.Addons
            .Select(addon => new ReplacementAddonItemViewModel(addon, status.IsEffective(addon), locate))
            .ToArray();
        SummaryText = string.Join("、", Addons.Select(addon => addon.Name));
    }

    public ReplacementSlot Slot { get; }

    public ReplacementSlotState State { get; }

    public string StateText { get; }

    public string DisplayName => Slot.DisplayName;

    public string SecondaryName { get; }

    public IReadOnlyList<ReplacementAddonItemViewModel> Addons { get; }

    public string? SummaryText { get; }

    private static string GetStateText(ReplacementSlotState state)
    {
        return state switch
        {
            ReplacementSlotState.Vanilla => Texts.ReplacementStateVanilla,
            ReplacementSlotState.InstalledButDisabled => Texts.ReplacementStateInstalledButDisabled,
            ReplacementSlotState.Replaced => Texts.ReplacementStateReplaced,
            ReplacementSlotState.Overridden => Texts.ReplacementStateOverridden,
            ReplacementSlotState.Conflict => Texts.ReplacementStateConflict,
            _ => Texts.ReplacementStateVanilla
        };
    }
}

public sealed partial class ReplacementAddonItemViewModel : ViewModelBase
{
    private readonly VpkAddon _addon;
    private readonly Action<VpkAddon> _locate;

    public ReplacementAddonItemViewModel(ReplacementAddonEntry entry, bool isEffective, Action<VpkAddon> locate)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(locate);

        _addon = entry.Addon;
        _locate = locate;

        Name = entry.Addon.Name;
        Priority = entry.Priority;
        IsEnabled = entry.IsEnabledInHierarchy;
        IsEffective = isEffective;
        RoleText = GetRoleText(entry, isEffective);
        DetailsText = string.Format(Texts.ReplacementAddonDetails, entry.Priority, entry.MatchedFileCount);
    }

    public string Name { get; }

    public int Priority { get; }

    public bool IsEnabled { get; }

    public bool IsEffective { get; }

    public string RoleText { get; }

    public string DetailsText { get; }

    /// <summary>Was <c>ReactiveCommand.Create(() =&gt; locate(entry.Addon))</c>.</summary>
    [RelayCommand]
    private void Locate()
    {
        _locate(_addon);
    }

    private static string GetRoleText(ReplacementAddonEntry entry, bool isEffective)
    {
        if (!entry.IsEnabledInHierarchy)
        {
            return Texts.ReplacementRoleDisabled;
        }
        return isEffective ? Texts.ReplacementRoleEffective : Texts.ReplacementRoleCovered;
    }
}

public sealed partial class ReplacementOverviewViewModel : ViewModelBase, IViewModelLifecycle, IValidity, IDisposable
{
    private static readonly (ReplacementSlotCategory Category, string Title)[] s_categoryTitles =
    [
        (ReplacementSlotCategory.PrimaryWeapon, Texts.ReplacementCategoryPrimaryWeapon),
        (ReplacementSlotCategory.SecondaryWeapon, Texts.ReplacementCategorySecondaryWeapon),
        (ReplacementSlotCategory.MeleeWeapon, Texts.ReplacementCategoryMeleeWeapon),
        (ReplacementSlotCategory.Equipment, Texts.ReplacementCategoryEquipment),
        (ReplacementSlotCategory.CommonInfected, Texts.ReplacementCategoryCommonInfected),
        (ReplacementSlotCategory.SpecialInfected, Texts.ReplacementCategorySpecialInfected)
    ];

    private readonly AddonRoot _addonRoot;
    private readonly Action<VpkAddon> _locateAddon;
    private readonly CancellationTokenSource _cts = new();

    private bool _disposed = false;
    private bool _isActive = false;
    private ReplacementOverviewFilterOption _selectedFilterOption;
    private ReplacementOverview? _overview = null;

    private IDisposable? _invalidHandler = null;

    public ReplacementOverviewViewModel(AddonRoot addonRoot, Action<VpkAddon> locateAddon)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);
        ArgumentNullException.ThrowIfNull(locateAddon);

        _addonRoot = addonRoot;
        _locateAddon = locateAddon;

        IsValid = true;
        IsRefreshing = false;
        Groups = [];
        HasNoVisibleSlots = true;
        Summary = "";

        FilterOptions =
        [
            new(Texts.ReplacementFilterAll, ReplacementOverviewFilter.All),
            new(Texts.ReplacementFilterReplaced, ReplacementOverviewFilter.Replaced),
            new(Texts.ReplacementFilterVanilla, ReplacementOverviewFilter.Vanilla),
            new(Texts.ReplacementFilterConflict, ReplacementOverviewFilter.Conflict),
            new(Texts.ReplacementFilterInstalledButDisabled, ReplacementOverviewFilter.InstalledButDisabled)
        ];
        _selectedFilterOption = FilterOptions[0];
    }

    [ObservableProperty]
    public partial bool IsValid { get; private set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; private set; }

    public IReadOnlyList<ReplacementOverviewFilterOption> FilterOptions { get; }

    public ReplacementOverviewFilterOption SelectedFilterOption
    {
        get => _selectedFilterOption;
        set
        {
            if (value is null || value == _selectedFilterOption)
            {
                return;
            }
            _selectedFilterOption = value;
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    [ObservableProperty]
    public partial IReadOnlyList<ReplacementCategoryGroupViewModel> Groups { get; private set; }

    [ObservableProperty]
    public partial bool HasNoVisibleSlots { get; private set; }

    [ObservableProperty]
    public partial string Summary { get; private set; }

    public void Activate()
    {
        if (_isActive)
        {
            return;
        }
        _isActive = true;

        _invalidHandler = _addonRoot.RegisterInvalidHandler(() => IsValid = false);
        if (!IsValid)
        {
            return;
        }

        RefreshCommand.Execute(null);
    }

    public void Deactivate()
    {
        if (!_isActive)
        {
            return;
        }
        _isActive = false;

        Utils.DisposeAndSetNull(ref _invalidHandler);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        IsValid = false;
        _cts.Cancel();
        _cts.Dispose();
        _disposed = true;
    }

    public void LocateAddon(VpkAddon addon)
    {
        ArgumentNullException.ThrowIfNull(addon);

        _locateAddon(addon);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        this.ThrowIfInvalid();

        if (IsRefreshing)
        {
            return;
        }

        IsRefreshing = true;
        try
        {
            var cancellationToken = _cts.Token;
            ReplacementOverview overview;
            try
            {
                overview = await ReplacementOverviewAnalyzer.AnalyzeAsync(_addonRoot, cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                var dialogService = AppServices.TryGetService<IDialogService>();
                if (dialogService is not null)
                {
                    await dialogService.ShowExceptionAsync(ex);
                }
                return;
            }

            if (!IsValid)
            {
                return;
            }

            _overview = overview;
            Summary = string.Format(
                Texts.ReplacementSummary,
                overview.Slots.Count,
                overview.ReplacedCount,
                overview.VanillaCount,
                overview.ConflictCount,
                overview.InstalledButDisabledCount);
            ApplyFilter();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void ApplyFilter()
    {
        var overview = _overview;
        if (overview is null)
        {
            Groups = [];
            HasNoVisibleSlots = true;
            return;
        }

        var filter = _selectedFilterOption.Filter;
        var groups = new List<ReplacementCategoryGroupViewModel>();
        foreach (var (category, title) in s_categoryTitles)
        {
            var items = overview.Slots
                .Where(status => status.Slot.Category == category && IsVisible(status, filter))
                .Select(status => new ReplacementSlotItemViewModel(status, LocateAddon))
                .ToArray();
            if (items.Length > 0)
            {
                groups.Add(new(title, items));
            }
        }

        Groups = groups;
        HasNoVisibleSlots = groups.Count == 0;
    }

    private static bool IsVisible(ReplacementSlotStatus status, ReplacementOverviewFilter filter)
    {
        return filter switch
        {
            ReplacementOverviewFilter.Replaced =>
                status.State is ReplacementSlotState.Replaced or ReplacementSlotState.Overridden,
            ReplacementOverviewFilter.Vanilla => status.State == ReplacementSlotState.Vanilla,
            ReplacementOverviewFilter.Conflict => status.State == ReplacementSlotState.Conflict,
            ReplacementOverviewFilter.InstalledButDisabled => status.State == ReplacementSlotState.InstalledButDisabled,
            _ => true
        };
    }
}
