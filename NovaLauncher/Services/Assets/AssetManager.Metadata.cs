using NovaLauncher.Models;
using NovaLauncher.Services.Images;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NovaLauncher.Services.Assets;

/// <summary>
/// Provides metadata synchronization and rebuilding functionality.
/// </summary>
public sealed partial class AssetManager
{
    /// <summary>
    /// Rebuilds all managed metadata by scanning the asset directory.
    /// </summary>
    public async Task<AssetMetadata> RebuildMetadataAsync(
        Game game,
        CancellationToken cancellationToken = default)
    {
        ValidateGame(game);

        AssetMetadata metadata =
            await LoadOrCreateMetadataAsync(
                game,
                cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        await RebuildArtworkMetadataAsync(
            game,
            metadata,
            cancellationToken);

        await RebuildSaveMetadataAsync(
            game,
            metadata,
            cancellationToken);

        await RebuildScreenshotMetadataAsync(
            game,
            metadata,
            cancellationToken);

        await RebuildVideoMetadataAsync(
            game,
            metadata,
            cancellationToken);

        await RebuildModMetadataAsync(
            game,
            metadata,
            cancellationToken);

        metadata.UpdatedAt =
            DateTimeOffset.UtcNow;

        await SaveMetadataAsync(
            game,
            metadata,
            cancellationToken);

        return metadata;
    }

    /// <summary>
    /// Rebuilds artwork metadata by reconciling the current metadata
    /// records with the artwork files that exist on disk.
    /// </summary>
    private async Task RebuildArtworkMetadataAsync(
        Game game,
        AssetMetadata metadata,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<ArtworkFileInfo> discoveredArtwork =
            await EnumerateArtworkFilesAsync(
                game,
                cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        SynchronizeArtworkMetadata(
            metadata.Artwork,
            discoveredArtwork);
    }

    private Task RebuildSaveMetadataAsync(
        Game game,
        AssetMetadata metadata,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    private Task RebuildScreenshotMetadataAsync(
        Game game,
        AssetMetadata metadata,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    private Task RebuildVideoMetadataAsync(
        Game game,
        AssetMetadata metadata,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    private Task RebuildModMetadataAsync(
        Game game,
        AssetMetadata metadata,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Enumerates every managed artwork file for a game.
    /// </summary>
    private async Task<IReadOnlyList<ArtworkFileInfo>>
        EnumerateArtworkFilesAsync(
            Game game,
            CancellationToken cancellationToken)
    {
        List<ArtworkFileInfo> artwork = [];

        await EnumerateArtworkFolderAsync(
            game,
            AssetFolder.ArtworkOriginal,
            artwork,
            cancellationToken);

        await EnumerateArtworkFolderAsync(
            game,
            AssetFolder.ArtworkGenerated,
            artwork,
            cancellationToken);

        await EnumerateArtworkFolderAsync(
            game,
            AssetFolder.ArtworkCustom,
            artwork,
            cancellationToken);

        await EnumerateArtworkFolderAsync(
            game,
            AssetFolder.ArtworkActive,
            artwork,
            cancellationToken);

        return artwork;
    }

    /// <summary>
    /// Synchronizes persisted artwork metadata with artwork discovered
    /// on disk.
    ///
    /// Existing records are preserved whenever their relative path still
    /// exists. Missing files are removed, and newly discovered files are
    /// added.
    /// </summary>
    private static void SynchronizeArtworkMetadata(
        ArtworkAssetMetadata artworkMetadata,
        IReadOnlyList<ArtworkFileInfo> discoveredArtwork)
    {
        ArgumentNullException.ThrowIfNull(artworkMetadata);
        ArgumentNullException.ThrowIfNull(discoveredArtwork);

        Dictionary<string, ArtworkAssetRecord> existingByPath =
            artworkMetadata.Items
                .Where(
                    record =>
                        !string.IsNullOrWhiteSpace(
                            record.RelativePath))
                .GroupBy(
                    record =>
                        NormalizeRelativePath(
                            record.RelativePath),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.First(),
                    StringComparer.OrdinalIgnoreCase);

        List<ArtworkAssetRecord> synchronizedItems = [];

        foreach (ArtworkFileInfo discoveredFile in discoveredArtwork)
        {
            string normalizedPath =
                NormalizeRelativePath(
                    discoveredFile.RelativePath);

            ArtworkAssetRecord record;

            if (existingByPath.TryGetValue(
                    normalizedPath,
                    out ArtworkAssetRecord? existingRecord))
            {
                record =
                    existingRecord;

                UpdateArtworkRecord(
                    record,
                    discoveredFile);
            }
            else
            {
                record =
                    CreateArtworkRecord(
                        discoveredFile);
            }

            synchronizedItems.Add(record);
        }

        artworkMetadata.Items =
            synchronizedItems
                .OrderBy(
                    item =>
                        item.RelativePath,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        UpdateArtworkSources(artworkMetadata);
        UpdateActiveArtworkPaths(artworkMetadata);
    }

    /// <summary>
    /// Normalizes a relative path for storage in metadata.
    /// </summary>
    private static string NormalizeRelativePath(
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return path
            .Replace(
                Path.DirectorySeparatorChar,
                '/')
            .Replace(
                Path.AltDirectorySeparatorChar,
                '/');
    }

    /// <summary>
    /// Rebuilds the list of known artwork sources.
    /// </summary>
    private static void UpdateArtworkSources(
        ArtworkAssetMetadata metadata)
    {
        metadata.Sources =
            metadata.Items
                .Select(item => item.Source)
                .Where(
                    source =>
                        !string.IsNullOrWhiteSpace(source))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(
                    source => source,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    /// <summary>
    /// Updates an existing artwork metadata record with values discovered
    /// from the current file on disk.
    /// </summary>
    private static void UpdateArtworkRecord(
        ArtworkAssetRecord record,
        ArtworkFileInfo artwork)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(artwork);

        record.Type =
            artwork.Type;

        record.RelativePath =
            NormalizeRelativePath(
                artwork.RelativePath);

        record.Width =
            artwork.Image.Width;

        record.Height =
            artwork.Image.Height;

        record.FileSizeBytes =
            artwork.Image.FileSizeBytes;

        record.FileHash =
            artwork.Image.Hash;

        record.HashAlgorithm =
            artwork.Image.HashAlgorithm;

        record.IsActive =
            artwork.IsActive;

        record.IsCustom =
            artwork.IsCustom;

        record.IsGenerated =
            artwork.IsGenerated;

        if (string.IsNullOrWhiteSpace(record.Source))
        {
            record.Source =
                DetermineArtworkSource(artwork);
        }
    }

    /// <summary>
    /// Creates a new artwork metadata record from an artwork file
    /// discovered on disk.
    /// </summary>
    private static ArtworkAssetRecord CreateArtworkRecord(
        ArtworkFileInfo artwork)
    {
        ArgumentNullException.ThrowIfNull(artwork);

        return new ArtworkAssetRecord
        {
            Type =
                artwork.Type,

            RelativePath =
                NormalizeRelativePath(
                    artwork.RelativePath),

            Width =
                artwork.Image.Width,

            Height =
                artwork.Image.Height,

            FileSizeBytes =
                artwork.Image.FileSizeBytes,

            FileHash =
                artwork.Image.Hash,

            HashAlgorithm =
                artwork.Image.HashAlgorithm,

            IsActive =
                artwork.IsActive,

            IsCustom =
                artwork.IsCustom,

            IsGenerated =
                artwork.IsGenerated,

            Source =
                DetermineArtworkSource(artwork)
        };
    }

    /// <summary>
    /// Determines the metadata source label for discovered artwork.
    /// </summary>
    private static string DetermineArtworkSource(
        ArtworkFileInfo artwork)
    {
        if (artwork.IsCustom)
        {
            return "Custom";
        }

        if (artwork.IsGenerated)
        {
            return "Generated";
        }

        if (artwork.IsActive)
        {
            return "Active";
        }

        return "Original";
    }

    private static void UpdateActiveArtworkPaths(
        ArtworkAssetMetadata metadata)
    {
        metadata.ActiveCoverPath =
            metadata.Items.FirstOrDefault(
                item =>
                    item.IsActive &&
                    item.Type == AssetType.Cover)?
                .RelativePath;

        metadata.ActiveHeroPath =
            metadata.Items.FirstOrDefault(
                item =>
                    item.IsActive &&
                    item.Type == AssetType.Hero)?
                .RelativePath;

        metadata.ActiveLogoPath =
            metadata.Items.FirstOrDefault(
                item =>
                    item.IsActive &&
                    item.Type == AssetType.Logo)?
                .RelativePath;

        metadata.ActiveThumbnailPath =
            metadata.Items.FirstOrDefault(
                item =>
                    item.IsActive &&
                    item.Type == AssetType.Thumbnail)?
                .RelativePath;

        metadata.ActiveBackgroundPath =
            metadata.Items.FirstOrDefault(
                item =>
                    item.IsActive &&
                    item.Type == AssetType.Background)?
                .RelativePath;
    }

    /// <summary>
    /// Enumerates and inspects every artwork file within one managed
    /// artwork folder.
    /// </summary>
    private async Task EnumerateArtworkFolderAsync(
        Game game,
        AssetFolder folder,
        ICollection<ArtworkFileInfo> artwork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artwork);

        string folderPath =
            _paths.GetFolderPath(
                game,
                folder);

        if (!Directory.Exists(folderPath))
        {
            return;
        }

        string gameRoot =
            _paths.GetGameRoot(game);

        IEnumerable<string> filePaths;

        try
        {
            filePaths =
                Directory.EnumerateFiles(
                    folderPath,
                    "*",
                    SearchOption.AllDirectories);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException)
        {
            Debug.WriteLine(
                $"Could not enumerate artwork folder " +
                $"'{folderPath}': {exception.Message}");

            return;
        }

        foreach (string filePath in filePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            FileInfo fileInfo =
                new(filePath);

            ImageInfo imageInfo;

            try
            {
                imageInfo =
                    await _imageInspector.InspectAsync(
                        fileInfo,
                        cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is IOException or
                UnauthorizedAccessException or
                InvalidDataException or
                NotSupportedException)
            {
                Debug.WriteLine(
                    $"Could not inspect artwork file " +
                    $"'{filePath}': {exception.Message}");

                continue;
            }

            string relativePath =
                Path.GetRelativePath(
                    gameRoot,
                    filePath);

            artwork.Add(
                new ArtworkFileInfo(
                    Type:
                        DetermineArtworkType(
                            fileInfo.Name),

                    Folder:
                        folder,

                    RelativePath:
                        NormalizeRelativePath(
                            relativePath),

                    AbsolutePath:
                        fileInfo.FullName,

                    IsActive:
                        folder ==
                        AssetFolder.ArtworkActive,

                    IsCustom:
                        folder ==
                        AssetFolder.ArtworkCustom,

                    IsGenerated:
                        folder ==
                        AssetFolder.ArtworkGenerated,

                    Image:
                        imageInfo,

                    LastModifiedAt:
                        fileInfo.LastWriteTimeUtc));
        }
    }

    /// <summary>
    /// Determines the artwork type from its filename.
    /// </summary>
    private static AssetType DetermineArtworkType(
        string filename)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);

        string name =
            Path.GetFileNameWithoutExtension(filename)
                .ToLowerInvariant();

        return name switch
        {
            "cover" =>
                AssetType.Cover,

            "hero" =>
                AssetType.Hero,

            "logo" =>
                AssetType.Logo,

            "thumbnail" =>
                AssetType.Thumbnail,

            "background" =>
                AssetType.Background,

            "icon" =>
                AssetType.Icon,

            _ =>
                AssetType.OriginalArtwork
        };
    }

    /// <summary>
    /// Represents an artwork file and the metadata discovered while
    /// inspecting it.
    /// </summary>
    private sealed record ArtworkFileInfo(
        AssetType Type,
        AssetFolder Folder,
        string RelativePath,
        string AbsolutePath,
        bool IsActive,
        bool IsCustom,
        bool IsGenerated,
        ImageInfo Image,
        DateTimeOffset LastModifiedAt);
}