using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NovaLauncher.Services.Artwork;

public sealed class ArtworkCache : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _artworkFolder;
    private bool _disposed;

    public ArtworkCache()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "NovaLauncher/0.1");

        string localApplicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        _artworkFolder = Path.Combine(
            localApplicationData,
            "NovaLauncher",
            "Artwork");

        Directory.CreateDirectory(_artworkFolder);
    }

    public async Task<string?> GetOrDownloadAsync(
        Uri artworkUri,
        string providerName,
        string gameIdentifier,
        string artworkType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artworkUri);

        string providerFolder = Path.Combine(
            _artworkFolder,
            MakeSafeFileName(providerName));

        Directory.CreateDirectory(providerFolder);

        string extension = Path.GetExtension(
            artworkUri.AbsolutePath);

        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".jpg";
        }

        string fileName =
            $"{MakeSafeFileName(gameIdentifier)}-" +
            $"{MakeSafeFileName(artworkType.ToLowerInvariant())}" +
            $"{extension}";

        string artworkPath = Path.Combine(
            providerFolder,
            fileName);

        if (File.Exists(artworkPath))
        {
            return artworkPath;
        }

        string temporaryPath = artworkPath + ".download";

        try
        {
            using HttpResponseMessage response =
                await _httpClient.GetAsync(
                    artworkUri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine(
                    $"Artwork download failed: {(int)response.StatusCode} " +
                    $"{response.ReasonPhrase} - {artworkUri}");

                return null;
            }

            string? mediaType =
                response.Content.Headers.ContentType?.MediaType;

            if (mediaType is not null &&
                !mediaType.StartsWith(
                    "image/",
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            await using Stream sourceStream =
                await response.Content.ReadAsStreamAsync(
        cancellationToken);

            await using (FileStream destinationStream =
                new(
                    temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
            {
                await sourceStream.CopyToAsync(
                    destinationStream,
                    cancellationToken);

                await destinationStream.FlushAsync(
                    cancellationToken);
            }

            File.Move(
                temporaryPath,
                artworkPath,
                overwrite: true);

            return artworkPath;
        }

        catch (OperationCanceledException)
        {
            DeleteTemporaryFile(temporaryPath);
            throw;
        }
        catch (HttpRequestException exception)
        {
            DeleteTemporaryFile(temporaryPath);

            Debug.WriteLine(
                $"Artwork HTTP error: {exception.Message}");

            return null;
        }
        catch (IOException exception)
        {
            DeleteTemporaryFile(temporaryPath);

            Debug.WriteLine(
                $"Artwork file error: {exception.Message}");

            return null;
        }
        catch (UnauthorizedAccessException exception)
        {
            DeleteTemporaryFile(temporaryPath);

            Debug.WriteLine(
                $"Artwork permission error: {exception.Message}");

            return null;
        }

    }

    private static string MakeSafeFileName(string value)
    {
        foreach (char invalidCharacter
                 in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(
                invalidCharacter,
                '_');
        }

        return value;
    }

    private static void DeleteTemporaryFile(
        string temporaryPath)
    {
        try
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
        catch
        {
            // Cleanup failure should not crash NovaLauncher.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _httpClient.Dispose();
        _disposed = true;
    }
}