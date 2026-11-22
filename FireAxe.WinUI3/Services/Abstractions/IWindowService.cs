// Ported from FireAxe_Alt\FireAxe.GUI\IAppWindowManager.cs + the window-opening Interactions
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FireAxe.Services;

/// <summary>
/// Replaces <c>IAppWindowManager</c> and the ~5 "show window" interactions
/// (<c>ShowImportResult</c>, <c>ShowNewWorkshopCollectionWindow</c>, <c>ShowDeletionProgress</c>, ...).
/// The ViewModel layer only knows this interface; P2 implements it on top of WinUI windows.
/// </summary>
public interface IWindowService
{
    void OpenSettingsWindow();

    void OpenDownloadListWindow();

    void OpenAboutWindow();

    void OpenProblemListWindow();

    void OpenTagManagerWindow();

    void OpenVpkConflictListWindow();

    void OpenReplacementOverviewWindow();

    void OpenWorkshopVpkFinderWindow();

    void OpenFileCleanerWindow();

    void OpenAddonNameAutoSetterWindow();

    void OpenFileNameFixerWindow();

    /// <summary>Shows the "new workshop collection" window (non-modal, like the Avalonia version).</summary>
    void OpenNewWorkshopCollectionWindow(AddonRoot addonRoot, AddonGroup? addonGroup);

    /// <summary>Shows the import result window (modal in the Avalonia version).</summary>
    Task ShowImportResultAsync(AddonRoot.ImportResult importResult);

    /// <summary>
    /// Shows the deletion progress window. Returns as soon as it is shown — the operations
    /// keep running in the background, exactly like the Avalonia handler which fired and returned.
    /// </summary>
    void ShowDeletionProgress(IEnumerable<(Task, string)> operations);

    // ---------------------------------------------------------------- P3 additions
    // The addon explorer needs three more windows that P4/P5 will port in full. They are declared here
    // (rather than built inside the View) so that the "no window is created outside the service" rule
    // from P2 still holds; WindowService answers with its usual "not ported yet" placeholder.

    /// <summary>Opens the tag editor for <paramref name="addon"/> (Alt: <c>AddonTagEditorWindow</c>).</summary>
    void OpenAddonTagEditorWindow(AddonNode addon);

    /// <summary>Opens the "customize preview image" window (Alt: <c>AddonNodeCustomizeImageWindow</c>).</summary>
    void OpenAddonNodeCustomizeImageWindow(AddonNode addon);

    /// <summary>
    /// Opens the addon picker (Alt: <c>AddonNodePickerWindow</c>) and returns the nodes the user picked,
    /// or <c>null</c> when the window was cancelled / is not available yet.
    /// </summary>
    Task<IReadOnlyList<AddonNode>?> PickAddonNodesAsync(AddonRoot addonRoot, bool allowMultiple, string title);

    /// <summary>Brings the main window to the foreground.</summary>
    void ActivateMainWindow();

    /// <summary>Switches the main window back to the primary Library page and brings it to the foreground.</summary>
    void ShowLibraryPage();
}
