// New in P2: replaces the Avalonia `ownerWindow` parameters that CommonMessageBoxes took.
using Microsoft.UI.Xaml;

namespace FireAxe.Services;

/// <summary>
/// Tells the UI services which window is currently in the foreground, so that dialogs and pickers
/// attach to (and center on) the window the user is looking at instead of always the main window.
/// </summary>
public interface IActiveWindowProvider
{
    /// <summary>The most recently activated window that is still open, or null before the first window exists.</summary>
    Window? ActiveWindow { get; }
}
