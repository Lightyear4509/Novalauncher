using System;
using System.Threading;
using System.Threading.Tasks;
using NovaLauncher.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
    private Task RebuildArtworkMetadataAsync(
        Game game,
        AssetMetadata metadata,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<ArtworkFileInfo> discoveredArtwork =
            EnumerateArtworkFiles(game);

        cancellationToken.ThrowIfCancellationRequested();

        SynchronizeArtworkMetadata(
            metadata.Artwork,
            discoveredArtwork);

        return Task.CompletedTask;
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
    private IReadOnlyList<ArtworkFileInfo> EnumerateArtworkFiles(
        Game game)
    {
        List<ArtworkFileInfo> artwork = [];

        EnumerateArtworkFolder(
            game,
            AssetFolder.ArtworkOriginal,
            artwork);

        EnumerateArtworkFolder(
            game,
            AssetFolder.ArtworkGenerated,
            artwork);

        EnumerateArtworkFolder(
            game,
            AssetFolder.ArtworkCustom,
            artwork);

        EnumerateArtworkFolder(
            game,
            AssetFolder.ArtworkActive,
            artwork);

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

        return path.Replace(
            Path.DirectorySeparatorChar,
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
                .Where(source =>
                    !string.IsNullOrWhiteSpace(source))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(source => source)
                .ToList();
    }

    private static void UpdateArtworkRecord(
     ArtworkAssetRecord record,
     ArtworkFileInfo artwork)
    {
        record.Type =
            artwork.Type;

        record.RelativePath =
            NormalizeRelativePath(
                artwork.RelativePath);

        record.FileSizeBytes =
            artwork.FileSizeBytes;

        record.IsActive =
            artwork.IsActive;

        record.IsCustom =
            artwork.IsCustom;

        record.IsGenerated =
            artwork.IsGenerated;
    }

    private static ArtworkAssetRecord CreateArtworkRecord(
    ArtworkFileInfo artwork)
    {
        return new ArtworkAssetRecord
        {
            Type =
                artwork.Type,

            RelativePath =
                NormalizeRelativePath(
                    artwork.RelativePath),

            FileSizeBytes =
                artwork.FileSizeBytes,

            IsActive =
                artwork.IsActive,

            IsCustom =
                artwork.IsCustom,

            IsGenerated =
                artwork.IsGenerated,

            Source =
                artwork.IsCustom
                    ? "Custom"
                    : artwork.IsGenerated
                        ? "Generated"
                        : "Unknown"
        };
    }

    private static void UpdateActiveArtworkPaths(
    ArtworkAssetMetadata metadata)
    {
        metadata.ActiveCoverPath =
            metadata.Items.FirstOrDefault(
                x => x.IsActive &&
                     x.Type == AssetType.Cover)?
                .RelativePath;

        metadata.ActiveHeroPath =
            metadata.Items.FirstOrDefault(
                x => x.IsActive &&
                     x.Type == AssetType.Hero)?
                .RelativePath;

        metadata.ActiveLogoPath =
            metadata.Items.FirstOrDefault(
                x => x.IsActive &&
                     x.Type == AssetType.Logo)?
                .RelativePath;

        metadata.ActiveThumbnailPath =
            metadata.Items.FirstOrDefault(
                x => x.IsActive &&
                     x.Type == AssetType.Thumbnail)?
                .RelativePath;

        metadata.ActiveBackgroundPath =
            metadata.Items.FirstOrDefault(
                x => x.IsActive &&
                     x.Type == AssetType.Background)?
                .RelativePath;
    }

    /// <summary>
    /// Enumerates every artwork file within one managed artwork folder.
    /// </summary>
    private void EnumerateArtworkFolder(
        Game game,
        AssetFolder folder,
        ICollection<ArtworkFileInfo> artwork)
    {
        string folderPath =
            _paths.GetFolderPath(
                game,
                folder);

        if (!Directory.Exists(folderPath))
        {
            return;
        }

        foreach (string filePath in Directory.EnumerateFiles(
                     folderPath,
                     "*",
                     SearchOption.AllDirectories))
        {
            FileInfo fileInfo =
                new(filePath);

            string relativePath =
                Path.GetRelativePath(
                    _paths.GetGameRoot(game),
                    filePath);

            artwork.Add(
                new ArtworkFileInfo(
                    Type: DetermineArtworkType(fileInfo.Name),
                    Folder: folder,
                    RelativePath: relativePath.Replace('\\', '/'),
                    AbsolutePath: filePath,
                    IsActive: folder == AssetFolder.ArtworkActive,
                    IsCustom: folder == AssetFolder.ArtworkCustom,
                    IsGenerated: folder == AssetFolder.ArtworkGenerated,
                    FileSizeBytes: fileInfo.Length));
        }
    }

    /// <summary>
    /// Determines the artwork type from its filename.
    /// </summary>
    private static AssetType DetermineArtworkType(
        string filename)
    {
        string name =
            Path.GetFileNameWithoutExtension(filename)
                .ToLowerInvariant();

        return name switch
        {
            "cover" => AssetType.Cover,
            "hero" => AssetType.Hero,
            "logo" => AssetType.Logo,
            "thumbnail" => AssetType.Thumbnail,
            "background" => AssetType.Background,
            "icon" => AssetType.Icon,

            _ => AssetType.OriginalArtwork
        };
    }

    private sealed record ArtworkFileInfo(
    AssetType Type,
    AssetFolder Folder,
    string RelativePath,
    string AbsolutePath,
    bool IsActive,
    bool IsCustom,
    bool IsGenerated,
    long FileSizeBytes);
}