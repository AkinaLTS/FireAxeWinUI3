// Ported from FireAxe_Alt\FireAxe.GUI\ViewModels\AddonNodeSimpleViewModel.cs (Avalonia Bitmap.DecodeToWidth)
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FireAxe.Services;

/// <summary>
/// A decoded image that the View can bind to an <c>Image.Source</c>.
/// The ViewModel layer keeps it as this opaque interface so no UI framework type leaks in;
/// the View unwraps it with a converter (see P3).
/// </summary>
public interface IImageSource : IDisposable
{
}

/// <summary>
/// Abstraction over image decoding. Replaces Avalonia's
/// <c>Bitmap.DecodeToWidth(stream, width)</c> in <c>AddonNodeSimpleViewModel</c>.
/// </summary>
public interface IImageLoader
{
    /// <summary>
    /// Decodes <paramref name="data"/> into an image no wider than
    /// <paramref name="decodingWidth"/>. Returns null when the data is not a decodable image.
    /// </summary>
    Task<IImageSource?> DecodeToWidthAsync(byte[] data, int decodingWidth, CancellationToken cancellationToken = default);
}
