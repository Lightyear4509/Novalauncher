using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NovaLauncher.Services.Images;

/// <summary>
/// Inspects encoded image files without fully decoding their pixels.
/// </summary>
public interface IImageInspector
{
    /// <summary>
    /// Reads the dimensions, size, and hash of an image file.
    /// </summary>
    Task<ImageInfo> InspectAsync(
        FileInfo file,
        CancellationToken cancellationToken = default);
}