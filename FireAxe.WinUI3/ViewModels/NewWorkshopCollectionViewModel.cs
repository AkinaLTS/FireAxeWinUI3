// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\NewWorkshopCollectionViewModel.cs
// Mechanisms replaced:
//   Interaction<Unit, Unit> ShowInvalidCollectionIdInteraction -> IDialogService.ShowInfoAsync(Texts.InvalidCollectionIdMessage, Texts.Error)
//   Interaction<Unit, Unit> ShowCreateFailedInteraction -> IDialogService.ShowInfoAsync(Texts.CreateCollectionFailedMessage, Texts.Error)
//     (both texts/titles taken verbatim from Views\NewWorkshopCollectionWindow.axaml.cs's RegisterHandler blocks;
//      the handlers are gone, so the ViewModel composes the message itself)
//   ReactiveCommand.CreateFromTask(CreateAsync) -> [RelayCommand] on the async method (generates CreateCommand, same name)
//   IActivatableViewModel + WhenActivated(this.RegisterInvalidHandler(...).DisposeWith + _active flag) -> IViewModelLifecycle
//   ViewModelActivator Activator -> dropped (ReactiveUI type; same as the other ported ViewModels)
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FireAxe.Resources;
using FireAxe.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FireAxe.ViewModels;

public sealed partial class NewWorkshopCollectionViewModel : ViewModelBase, IViewModelLifecycle, IValidity, IDisposable
{
    private readonly AddonRoot _addonRoot;
    private readonly ValidRef<AddonGroup>? _addonGroupRef = null;

    private CancellationTokenSource? _createCts = null;

    private bool _created = false;
    private bool _active = false;
    private bool _isActive = false;
    private bool _disposed = false;

    private IDisposable? _invalidHandler = null;

    public NewWorkshopCollectionViewModel(AddonRoot addonRoot, AddonGroup? addonGroup)
    {
        ArgumentNullException.ThrowIfNull(addonRoot);
        _addonRoot = addonRoot;
        if (addonGroup is not null)
        {
            _addonGroupRef = new(addonGroup);
        }

        // Initial values (a partial property cannot carry an initializer).
        CollectionId = "";
        IncludeLinkedCollections = true;
        IsValid = true;
        IsCreating = false;
    }

    public event Action? CloseRequested = null;

    [ObservableProperty]
    public partial bool IsValid { get; private set; }

    [ObservableProperty]
    public partial bool IsCreating { get; private set; }

    [ObservableProperty]
    public partial string CollectionId { get; set; }

    [ObservableProperty]
    public partial bool IncludeLinkedCollections { get; set; }

    /// <summary>
    /// Called by the View when it is attached to the visual tree
    /// (was ReactiveUI's <c>WhenActivated</c>, which registered the invalid handler and set <c>_active</c>).
    /// </summary>
    public void Activate()
    {
        if (_isActive || _disposed)
        {
            return;
        }
        _isActive = true;

        _invalidHandler = _addonRoot.RegisterInvalidHandler(() => IsValid = false);
        if (!IsValid)
        {
            return;
        }

        _active = true;
    }

    /// <summary>
    /// Called by the View when it is detached (was the end of <c>WhenActivated</c>: disposed the invalid
    /// handler and cleared <c>_active</c>).
    /// </summary>
    public void Deactivate()
    {
        if (!_isActive)
        {
            return;
        }
        _isActive = false;

        Utils.DisposeAndSetNull(ref _invalidHandler);
        _active = false;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            IsValid = false;

            CancelCreate();

            _disposed = true;
        }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        this.ThrowIfInvalid();

        if (IsCreating)
        {
            return;
        }
        if (_created)
        {
            return;
        }

        IsCreating = true;
        try
        {
            ulong collectionId;
            if (!PublishedFileUtils.TryParsePublishedFileId(CollectionId, out collectionId))
            {
                await ShowInvalidCollectionIdAsync();
                return;
            }

            _createCts ??= new();

            ulong[]? itemIds = null;
            PublishedFileDetails? collectionDetails = null;
            var httpClient = _addonRoot.HttpClient;
            try
            {
                var itemIdsTask = WorkshopCollectionUtils.GetWorkshopCollectionContentAsync(collectionId, IncludeLinkedCollections, httpClient, _createCts.Token);
                var collectionDetailsTask = PublishedFileUtils.GetPublishedFileDetailsAsync(collectionId, httpClient, _createCts.Token);
                itemIds = await itemIdsTask;
                var getCollectionDetailsResult = await collectionDetailsTask;
                // The collection itself is reported as an image entry, so it's not downloadable,
                // but its details still carry the title we use for the new group.
                collectionDetails = getCollectionDetailsResult.Details;
            }
            catch (OperationCanceledException) { }

            if (itemIds == null || collectionDetails == null)
            {
                if (_active)
                {
                    await ShowCreateFailedAsync();
                }
                return;
            }

            CloseRequested?.Invoke();

            if (!_addonRoot.IsValid)
            {
                return;
            }

            var collectionGroup = AddonNode.Create<AddonGroup>(_addonRoot, _addonGroupRef?.TryGet());
            var collectionName = collectionGroup.Parent.GetUniqueChildName(collectionDetails.Title);
            collectionGroup.Name = collectionName;
            foreach (var itemId in itemIds)
            {
                var addon = AddonNode.Create<WorkshopVpkAddon>(_addonRoot, collectionGroup);
                addon.Name = addon.Parent.GetUniqueChildName(Texts.UnnamedWorkshopAddon);
                addon.RequestAutoSetName = true;
                addon.PublishedFileId = itemId;
            }

            _created = true;
        }
        finally
        {
            IsCreating = false;
        }
    }

    private void CancelCreate()
    {
        if (_createCts is not null)
        {
            _createCts.Cancel();
            _createCts.Dispose();
            _createCts = null;
        }
    }

    /// <summary>
    /// Replaces <c>ShowInvalidCollectionIdInteraction</c>
    /// (was <c>CommonMessageBoxes.ShowInfo(this, Texts.InvalidCollectionIdMessage, Texts.Error)</c>).
    /// </summary>
    private static async Task ShowInvalidCollectionIdAsync()
    {
        var dialogService = AppServices.TryGetService<IDialogService>();
        if (dialogService is null)
        {
            return;
        }
        await dialogService.ShowInfoAsync(Texts.InvalidCollectionIdMessage, Texts.Error);
    }

    /// <summary>
    /// Replaces <c>ShowCreateFailedInteraction</c>
    /// (was <c>CommonMessageBoxes.ShowInfo(this, Texts.CreateCollectionFailedMessage, Texts.Error)</c>).
    /// </summary>
    private static async Task ShowCreateFailedAsync()
    {
        var dialogService = AppServices.TryGetService<IDialogService>();
        if (dialogService is null)
        {
            return;
        }
        await dialogService.ShowInfoAsync(Texts.CreateCollectionFailedMessage, Texts.Error);
    }
}
