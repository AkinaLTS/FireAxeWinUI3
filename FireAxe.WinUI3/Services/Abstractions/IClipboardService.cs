// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\MainWindowViewModel.cs (Avalonia Clipboard usage)
using System.Threading.Tasks;

namespace FireAxe.Services;

/// <summary>
/// Abstraction over the system clipboard. Replaces Avalonia's
/// <c>MainWindow.Clipboard</c> usage in <c>MainWindowViewModel.CheckClipboard</c>.
/// </summary>
public interface IClipboardService
{
    /// <summary>Returns the clipboard text, or null when it is empty/unavailable.</summary>
    Task<string?> TryGetTextAsync();

    /// <summary>Puts <paramref name="text"/> on the clipboard.</summary>
    Task SetTextAsync(string text);
}
