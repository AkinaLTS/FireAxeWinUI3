// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\WorkshopVpkAddonViewModel.cs
using CommunityToolkit.Mvvm.Input;
using FireAxe.Resources;
using FireAxe.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FireAxe.ViewModels;

public partial class WorkshopVpkAddonViewModel : VpkAddonViewModel
{
    private DownloadItemViewModel? _downloadItemViewModel = null;

    private PublishedFileDetails? _publishedFileDetails = null;

    public WorkshopVpkAddonViewModel(WorkshopVpkAddon addon) : base(addon)
    {
    }

    public WorkshopVpkAddon? WorkshopAddon => (WorkshopVpkAddon?)((AddonNodeViewModel)this).Addon;

    public override Type AddonType => typeof(WorkshopVpkAddon);

    public string ItemId
    {
        get => WorkshopAddon?.PublishedFileId?.ToString() ?? "";
        set
        {
            if (!PublishedFileUtils.TryParsePublishedFileId(value, out var id))
            {
                throw new ArgumentException(Texts.InvalidPublishedFileIdMessage);
            }

            var addon = WorkshopAddon;
            if (addon == null)
            {
                return;
            }

            addon.PublishedFileId = id;
        }
    }

    public string DisplayItemId => ItemId.Length == 0 ? Texts.Null : ItemId;

    public string WorkshopTagsString
    {
        get
        {
            var tags = PublishedFileDetails?.Tags?.Where(tagObj => tagObj is not null).Select(tagObj => tagObj!.Tag);
            if (tags is null)
            {
                return "";
            }
            return string.Join(", ", tags);
        }
    }

    public DownloadItemViewModel? DownloadItemViewModel
    {
        get => _downloadItemViewModel;
        private set => SetProperty(ref _downloadItemViewModel, value);
    }

    public PublishedFileDetails? PublishedFileDetails
    {
        get => _publishedFileDetails;
        private set
        {
            if (SetProperty(ref _publishedFileDetails, value))
            {
                OnPropertyChanged(nameof(WorkshopTagsString));
            }
        }
    }

    [RelayCommand]
    private void ApplyTagsFromWorkshop()
    {
        var addon = WorkshopAddon;
        if (addon == null)
        {
            return;
        }

        addon.RequestApplyTagsFromWorkshop = true;
        addon.Check();
    }

    [RelayCommand]
    private async Task DeleteRedundantVpkFilesAsync()
    {
        var addon = WorkshopAddon;
        if (addon == null)
        {
            return;
        }

        try
        {
            var dialogService = AppServices.GetRequiredService<IDialogService>();
            var report = addon.RequestDeleteRedundantVpkFiles();
            bool confirm = await RedundantVpkFileDialogs.ConfirmDeleteAsync(dialogService, report);
            if (report.IsEmpty || !confirm)
            {
                return;
            }
            report.Execute();
            await RedundantVpkFileDialogs.ShowDeleteSuccessAsync(dialogService, report);
        }
        catch (Exception ex)
        {
            await ShowExceptionAsync(ex);
        }
    }

    public void OpenWorkshopPage()
    {
        if (WorkshopAddon?.PublishedFileId is { } id)
        {
            Utils.OpenWorkshopPage(id);
        }
    }

    protected override void OnNewAddon(AddonNode addon0, AddonSubscriptions subscriptions)
    {
        base.OnNewAddon(addon0, subscriptions);

        var addon = (WorkshopVpkAddon)addon0;

        subscriptions.Add(addon, nameof(WorkshopVpkAddon.PublishedFileId), () => OnPropertyChanged(nameof(ItemId)));

        void UpdateDownloadItemViewModel()
        {
            var downloadItem = addon.DownloadItem;
            DownloadItemViewModel = downloadItem is null ? null : new DownloadItemViewModel(downloadItem);
        }

        UpdateDownloadItemViewModel();
        subscriptions.Add(addon, nameof(WorkshopVpkAddon.DownloadItem), UpdateDownloadItemViewModel);
    }

    protected override void OnNullAddon()
    {
        base.OnNullAddon();

        OnPropertyChanged(nameof(ItemId));
        DownloadItemViewModel = null;
    }

    protected override void OnRefresh(CancellationToken cancellationToken)
    {
        base.OnRefresh(cancellationToken);

        var addon = WorkshopAddon;
        var refreshId = CurrentRefreshId;

        async void RefreshPublishedFileDetails()
        {
            PublishedFileDetails = null;

            if (addon == null)
            {
                return;
            }

            try
            {
                var result = await addon.GetPublishedFileDetailsAllowCacheAsync(cancellationToken);
                if (refreshId != CurrentRefreshId)
                {
                    return;
                }
                PublishedFileDetails = result;
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        RefreshPublishedFileDetails();
    }

    protected override void OnClearCaches()
    {
        base.OnClearCaches();

        PublishedFileDetails = null;
    }

    private async Task ShowExceptionAsync(Exception exception)
    {
        if (AppServices.TryGetService<IDialogService>() is { } dialogService)
        {
            await dialogService.ShowExceptionAsync(exception);
        }
    }
}
