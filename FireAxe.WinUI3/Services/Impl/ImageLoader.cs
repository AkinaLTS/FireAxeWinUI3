// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\AddonNodeSimpleViewModel.cs (Avalonia Bitmap.DecodeToWidth)
// Avalonia Bitmap.DecodeToWidth(stream, width) -> Microsoft.UI.Xaml.Media.Imaging.BitmapImage.DecodePixelWidth
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using Serilog;
using Windows.Storage.Streams;

namespace FireAxe.Services;

/// <summary>
/// <see cref="IImageLoader"/> over <see cref="BitmapImage"/>.
/// </summary>
/// <remarks>
/// <see cref="BitmapImage"/> is a XAML object, so creation and decoding both happen on the UI thread.
/// The ViewModel layer only ever sees <see cref="IImageSource"/>.
/// </remarks>
public sealed class ImageLoader : IImageLoader
{
    private readonly IUiDispatcher _dispatcher;

    public ImageLoader(IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
    }

    public async Task<IImageSource?> DecodeToWidthAsync(byte[] data, int decodingWidth, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(decodingWidth, 0);

        IImageSource? result = null;
        await _dispatcher.InvokeAsync(async () =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var stream = new InMemoryRandomAccessStream();
                using (var writer = new DataWriter(stream))
                {
                    writer.WriteBytes(data);
                    await writer.StoreAsync().AsTask(cancellationToken);
                    await writer.FlushAsync().AsTask(cancellationToken);
                    writer.DetachStream();
                }
                stream.Seek(0);

                var bitmap = new BitmapImage
                {
                    // Avalonia's DecodeToWidth counts decoded pixels, which is the physical size here.
                    DecodePixelType = DecodePixelType.Physical,
                    DecodePixelWidth = decodingWidth,
                };
                await bitmap.SetSourceAsync(stream).AsTask(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                result = new BitmapImageSource(bitmap);
            }
            catch (OperationCanceledException)
            {
                result = null;
            }
            catch (Exception ex)
            {
                // Undecodable data is a normal outcome (corrupt preview image), not a crash.
                Log.Error(ex, "Exception occurred during ImageLoader.DecodeToWidthAsync.");
                result = null;
            }
        });
        return result;
    }

    /// <summary>
    /// Unwraps <see cref="IImageSource"/> for an <c>Image.Source</c> binding (used by the Views from P3 on).
    /// Returns null for null and for foreign implementations.
    /// </summary>
    public static Microsoft.UI.Xaml.Media.ImageSource? ToImageSource(IImageSource? source)
    {
        return (source as BitmapImageSource)?.Bitmap;
    }

    private sealed class BitmapImageSource : IImageSource
    {
        private BitmapImage? _bitmap;

        internal BitmapImageSource(BitmapImage bitmap)
        {
            _bitmap = bitmap;
        }

        internal BitmapImage? Bitmap => _bitmap;

        public void Dispose()
        {
            _bitmap = null;
        }
    }
}
