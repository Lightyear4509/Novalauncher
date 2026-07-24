using SkiaSharp;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace NovaLauncher.Services.Images;

/// <summary>
/// Inspects image files using SkiaSharp and SHA-256.
/// </summary>
public sealed class ImageInspector : IImageInspector
{
    private const string Sha256AlgorithmName = "SHA256";

    /// <inheritdoc />
    public async Task<ImageInfo> InspectAsync(
        FileInfo file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        cancellationToken.ThrowIfCancellationRequested();

        file.Refresh();

        if (!file.Exists)
        {
            throw new FileNotFoundException(
                "The image file could not be found.",
                file.FullName);
        }

        if (file.Length == 0)
        {
            throw new InvalidDataException(
                $"The image file is empty: '{file.FullName}'.");
        }

        (int width, int height) =
            ReadDimensions(file);

        string hash =
            await ComputeHashAsync(
                file,
                cancellationToken);

        return new ImageInfo
        {
            Width = width,
            Height = height,
            FileSizeBytes = file.Length,
            Hash = hash,
            HashAlgorithm = Sha256AlgorithmName
        };
    }

    /// <summary>
    /// Reads an encoded image's dimensions without decoding the full bitmap.
    /// </summary>
    private static (int Width, int Height) ReadDimensions(
        FileInfo file)
    {
        SKCodecResult codecResult;

        using SKCodec? codec =
            SKCodec.Create(
                file.FullName,
                out codecResult);

        if (codec is null)
        {
            throw new InvalidDataException(
                $"The file is not a supported or valid image: " +
                $"'{file.FullName}'. Skia result: {codecResult}.");
        }

        int width =
            codec.Info.Width;

        int height =
            codec.Info.Height;

        if (width <= 0 ||
            height <= 0)
        {
            throw new InvalidDataException(
                $"The image contains invalid dimensions: " +
                $"'{file.FullName}'.");
        }

        return (
            width,
            height);
    }

    /// <summary>
    /// Computes the SHA-256 hash of the complete image file.
    /// </summary>
    private static async Task<string> ComputeHashAsync(
        FileInfo file,
        CancellationToken cancellationToken)
    {
        await using FileStream stream =
            new(
                file.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);

        byte[] hash =
            await SHA256.HashDataAsync(
                stream,
                cancellationToken);

        return Convert.ToHexString(hash);
    }
}