// Ported from FireAxe_Alt\FireAxe.GUI\Views\MainWindow.axaml.cs + Views\WorkshopVpkAddonSectionView.axaml.cs
// The same two dialogs were registered in two different Views for the same report type;
// with the Interactions gone the composition lives here once.
using FireAxe.Resources;
using FireAxe.Services;
using System.Threading.Tasks;

namespace FireAxe.ViewModels;

internal static class RedundantVpkFileDialogs
{
    /// <summary>
    /// Asks whether the redundant VPK files described by <paramref name="report"/> may be deleted.
    /// Returns false (after telling the user) when there is nothing to delete.
    /// </summary>
    public static async Task<bool> ConfirmDeleteAsync(IDialogService dialogService, WorkshopVpkAddon.DeleteRedundantVpkFilesReport report)
    {
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(report);

        if (report.IsEmpty)
        {
            await dialogService.ShowInfoAsync(Texts.WorkshopVpkAddonDeleteRedundantVpkFilesReportEmptyMessage);
            return false;
        }

        var count = report.Files.Count;
        var totalFileSize = Utils.GetReadableBytes(report.TotalFileSize);
        return await dialogService.ConfirmAsync(
            Texts.WorkshopVpkAddonDeleteRedundantVpkFilesReportConfirmMessage.FormatNoThrow(count, totalFileSize));
    }

    /// <summary>Reports a completed redundant-VPK-file deletion.</summary>
    public static Task ShowDeleteSuccessAsync(IDialogService dialogService, WorkshopVpkAddon.DeleteRedundantVpkFilesReport report)
    {
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(report);

        var count = report.Files.Count;
        var totalFileSize = Utils.GetReadableBytes(report.TotalFileSize);
        return dialogService.ShowInfoAsync(
            Texts.WorkshopVpkAddonDeleteRedundantVpkFilesReportSuccessMessage.FormatNoThrow(count, totalFileSize));
    }
}
