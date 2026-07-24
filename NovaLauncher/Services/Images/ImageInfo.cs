namespace NovaLauncher.Services.Images;

/// <summary>
/// Contains metadata discovered from an image file.
/// </summary>
public sealed class ImageInfo
{
    /// <summary>
    /// Gets the image width in pixels.
    /// </summary>
    public int Width
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the image height in pixels.
    /// </summary>
    public int Height
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the image file size in bytes.
    /// </summary>
    public long FileSizeBytes
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the hexadecimal hash of the complete image file.
    /// </summary>
    public string Hash
    {
        get;
        init;
    } = string.Empty;

    /// <summary>
    /// Gets the algorithm used to calculate <see cref="Hash"/>.
    /// </summary>
    public string HashAlgorithm
    {
        get;
        init;
    } = string.Empty;
}