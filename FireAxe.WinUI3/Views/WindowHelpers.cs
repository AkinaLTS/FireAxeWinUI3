// New in P2: client-size helpers for the fixed-size Avalonia windows that used SizeToContent="WidthAndHeight".
// AppWindow works in physical pixels, the Avalonia XAML was in device-independent units.
using System;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace FireAxe.Views;

internal static class WindowHelpers
{
    /// <summary>Resizes the window's client area to the given device-independent size (after its content loaded).</summary>
    public static void SetClientSizeInDip(Window window, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(window);

        void Apply()
        {
            if ((window.Content as FrameworkElement)?.XamlRoot is not { } xamlRoot)
            {
                return;
            }

            double scale = xamlRoot.RasterizationScale > 0 ? xamlRoot.RasterizationScale : 1.0;
            window.AppWindow.ResizeClient(new SizeInt32(
                (int)Math.Round(width * scale),
                (int)Math.Round(height * scale)));
        }

        if ((window.Content as FrameworkElement) is { } content)
        {
            if (content.IsLoaded)
            {
                Apply();
            }
            else
            {
                content.Loaded += (sender, args) => Apply();
            }
        }
    }
}
