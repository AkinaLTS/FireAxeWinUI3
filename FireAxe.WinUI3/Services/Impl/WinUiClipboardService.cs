// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\MainWindowViewModel.cs (Avalonia Clipboard.GetTextAsync/SetTextAsync)
// Avalonia Clipboard -> Windows.ApplicationModel.DataTransfer.Clipboard (WinRT; both need the UI thread).
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace FireAxe.Services;

/// <summary>
/// <see cref="IClipboardService"/> over the WinRT clipboard API.
/// </summary>
public sealed class WinUiClipboardService : IClipboardService
{
    private readonly IUiDispatcher _dispatcher;

    public WinUiClipboardService(IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
    }

    public async Task<string?> TryGetTextAsync()
    {
        string? text = null;
        await _dispatcher.InvokeAsync(async () =>
        {
            var dataPackageView = Clipboard.GetContent();
            if (dataPackageView.Contains(StandardDataFormats.Text))
            {
                text = await dataPackageView.GetTextAsync();
            }
        });
        return text;
    }

    public Task SetTextAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return _dispatcher.InvokeAsync(() =>
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);
        });
    }
}
