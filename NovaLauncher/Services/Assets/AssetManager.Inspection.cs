using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NovaLauncher.Models;

namespace NovaLauncher.Services.Assets;

/// <summary>
/// Provides inspection and diagnostics functionality for AssetManager.
/// </summary>
public sealed partial class AssetManager
{
    /// <summary>
    /// Inspects the complete managed asset state for a game.
    ///
    /// This method does not create, delete, move, or repair files.
    /// It only examines the current filesystem state.
    /// </summary>
    public async Task<GameAssetState> InspectGameAsync(
        Game game,
        CancellationToken cancellationToken = default)
    {
        ValidateGame(game);

        cancellationToken.ThrowIfCancellationRequested();

        string gameRootPath =
            _paths.GetGameRoot(game);

        if (!Directory.Exists(gameRootPath))
        {
            return GameAssetState.CreateUninitialized(
                game.Id,
                game.Name,
                gameRootPath);
        }

        List<string> issues = [];
        HashSet<AssetRepairAction> repairActions = [];

        bool hasRequiredFolders =
            InspectRequiredFolders(
                game,
                issues,
                repairActions);

        OwnershipInspection ownership =
            await InspectOwnershipFilesAsync(
                game,
                issues,
                repairActions,
                cancellationToken);

        ArtworkInspection artwork =
            InspectArtwork(
                game,
                issues,
                repairActions);

        AssetCountInspection counts =
            InspectAssetCounts(
                game,
                issues,
                repairActions);

        StorageInspection storage =
            InspectStorage(
                gameRootPath,
                issues,
                cancellationToken);

        return new GameAssetState
        {
            GameId =
                game.Id,

            GameName =
                NormalizeInspectionGameName(game.Name),

            GameRootPath =
                gameRootPath,

            HasGameRoot =
                true,

            HasRequiredFolders =
                hasRequiredFolders,

            HasMetadata =
                ownership.HasMetadata,

            HasValidMetadata =
                ownership.HasValidMetadata,

            HasManifest =
                ownership.HasManifest,

            HasValidManifest =
                ownership.HasValidManifest,

            HasCover =
                artwork.HasCover,

            HasHero =
                artwork.HasHero,

            HasLogo =
                artwork.HasLogo,

            HasThumbnail =
                artwork.HasThumbnail,

            HasBackground =
                artwork.HasBackground,

            HasIcon =
                artwork.HasIcon,

            OriginalArtworkCount =
                counts.OriginalArtworkCount,

            GeneratedArtworkCount =
                counts.GeneratedArtworkCount,

            CustomArtworkCount =
                counts.CustomArtworkCount,

            ActiveArtworkCount =
                counts.ActiveArtworkCount,

            ScreenshotCount =
                counts.ScreenshotCount,

            VideoCount =
                counts.VideoCount,

            SaveBackupCount =
                counts.SaveBackupCount,

            ModFileCount =
                counts.ModFileCount,

            TotalSizeBytes =
                storage.TotalSizeBytes,

            LastAssetActivityAt =
                storage.LastAssetActivityAt,

            Metadata =
                ownership.Metadata,

            Issues =
                issues.ToArray(),

            RepairActions =
                repairActions.ToArray()
        };
    }

    /// <summary>
    /// Inspects several games sequentially.
    ///
    /// Sequential inspection avoids overwhelming slower disks with
    /// multiple recursive directory enumerations at the same time.
    /// </summary>
    public async Task<IReadOnlyList<GameAssetState>>
        InspectGamesAsync(
            IEnumerable<Game> games,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(games);

        List<GameAssetState> results = [];

        foreach (Game game in games)
        {
            cancellationToken.ThrowIfCancellationRequested();

            GameAssetState state =
                await InspectGameAsync(
                    game,
                    cancellationToken);

            results.Add(state);
        }

        return results;
    }

    /// <summary>
    /// Checks whether every required managed folder exists.
    /// </summary>
    private bool InspectRequiredFolders(
        Game game,
        ICollection<string> issues,
        ISet<AssetRepairAction> repairActions)
    {
        bool hasEveryFolder = true;

        foreach (AssetFolder folder in RequiredGameFolders)
        {
            string folderPath =
                _paths.GetFolderPath(
                    game,
                    folder);

            if (Directory.Exists(folderPath))
            {
                continue;
            }

            hasEveryFolder = false;

            issues.Add(
                $"The required asset folder '{folder}' is missing.");

            repairActions.Add(
                AssetRepairAction.CreateMissingFolders);
        }

        return hasEveryFolder;
    }

    /// <summary>
    /// Inspects metadata.json and the .nova ownership manifest.
    /// </summary>
    private async Task<OwnershipInspection>
        InspectOwnershipFilesAsync(
            Game game,
            ICollection<string> issues,
            ISet<AssetRepairAction> repairActions,
            CancellationToken cancellationToken)
    {
        string metadataPath =
            _paths.GetMetadataPath(game);

        string manifestPath =
            _paths.GetManifestPath(game);

        bool hasMetadata =
            File.Exists(metadataPath);

        bool hasManifest =
            File.Exists(manifestPath);

        bool hasValidMetadata = false;
        bool hasValidManifest = false;

        AssetMetadata? metadata = null;

        if (!hasMetadata)
        {
            issues.Add(
                "The asset metadata file is missing.");

            repairActions.Add(
                AssetRepairAction.CreateMetadata);
        }
        else
        {
            try
            {
                metadata =
                    await LoadMetadataAsync(
                        game,
                        cancellationToken);

                hasValidMetadata =
                    metadata is not null &&
                    metadata.GameId == game.Id &&
                    metadata.SchemaVersion > 0;

                if (!hasValidMetadata)
                {
                    issues.Add(
                        "The asset metadata file is invalid.");

                    repairActions.Add(
                        AssetRepairAction.RepairMetadata);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is IOException or
                UnauthorizedAccessException or
                InvalidDataException or
                JsonException)
            {
                issues.Add(
                    $"The asset metadata file could not be read: " +
                    $"{exception.Message}");

                repairActions.Add(
                    AssetRepairAction.RepairMetadata);
            }
        }

        if (!hasManifest)
        {
            issues.Add(
                "The Nova asset manifest is missing.");

            repairActions.Add(
                AssetRepairAction.CreateManifest);
        }
        else
        {
            try
            {
                hasValidManifest =
                    await HasValidManifestAsync(
                        game,
                        cancellationToken);

                if (!hasValidManifest)
                {
                    issues.Add(
                        "The Nova asset manifest is invalid or belongs " +
                        "to another game.");

                    repairActions.Add(
                        AssetRepairAction.RepairManifest);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is IOException or
                UnauthorizedAccessException or
                InvalidDataException or
                JsonException)
            {
                issues.Add(
                    $"The Nova asset manifest could not be read: " +
                    $"{exception.Message}");

                repairActions.Add(
                    AssetRepairAction.RepairManifest);
            }
        }

        return new OwnershipInspection(
            HasMetadata: hasMetadata,
            HasValidMetadata: hasValidMetadata,
            HasManifest: hasManifest,
            HasValidManifest: hasValidManifest,
            Metadata: metadata);
    }

    /// <summary>
    /// Inspects active artwork and icon availability.
    /// </summary>
    private ArtworkInspection InspectArtwork(
        Game game,
        ICollection<string> issues,
        ISet<AssetRepairAction> repairActions)
    {
        bool hasCover =
            File.Exists(
                _paths.GetActiveArtworkPath(
                    game,
                    AssetType.Cover));

        bool hasHero =
            File.Exists(
                _paths.GetActiveArtworkPath(
                    game,
                    AssetType.Hero));

        bool hasLogo =
            File.Exists(
                _paths.GetActiveArtworkPath(
                    game,
                    AssetType.Logo));

        bool hasThumbnail =
            File.Exists(
                _paths.GetActiveArtworkPath(
                    game,
                    AssetType.Thumbnail));

        bool hasBackground =
            File.Exists(
                _paths.GetActiveArtworkPath(
                    game,
                    AssetType.Background));

        bool hasIcon =
            File.Exists(
                _paths.GetAssetPath(
                    game,
                    AssetFolder.Icons,
                    AssetType.Icon));

        if (!hasCover)
        {
            issues.Add(
                "Active cover artwork is missing.");

            repairActions.Add(
                AssetRepairAction.AcquireCover);
        }

        if (!hasHero)
        {
            issues.Add(
                "Active hero artwork is missing.");

            repairActions.Add(
                AssetRepairAction.AcquireHero);
        }

        if (!hasThumbnail)
        {
            issues.Add(
                "The optimized artwork thumbnail is missing.");

            repairActions.Add(
                AssetRepairAction.GenerateThumbnail);
        }

        if (!hasLogo)
        {
            issues.Add(
                "Active logo artwork is missing.");

            repairActions.Add(
                AssetRepairAction.AcquireLogo);
        }

        if (!hasBackground)
        {
            issues.Add(
                "The processed background artwork is missing.");

            repairActions.Add(
                AssetRepairAction.GenerateBackground);
        }

        if (!hasIcon)
        {
            issues.Add(
                "The managed game icon is missing.");

            repairActions.Add(
                AssetRepairAction.AcquireIcon);
        }

        return new ArtworkInspection(
            HasCover: hasCover,
            HasHero: hasHero,
            HasLogo: hasLogo,
            HasThumbnail: hasThumbnail,
            HasBackground: hasBackground,
            HasIcon: hasIcon);
    }

    /// <summary>
    /// Counts managed artwork, media, saves, mods, and temporary files.
    /// </summary>
    private AssetCountInspection InspectAssetCounts(
        Game game,
        ICollection<string> issues,
        ISet<AssetRepairAction> repairActions)
    {
        int originalArtworkCount =
            CountFilesSafely(
                _paths.GetFolderPath(
                    game,
                    AssetFolder.ArtworkOriginal),
                SearchOption.AllDirectories,
                issues);

        int generatedArtworkCount =
            CountFilesSafely(
                _paths.GetFolderPath(
                    game,
                    AssetFolder.ArtworkGenerated),
                SearchOption.AllDirectories,
                issues);

        int customArtworkCount =
            CountFilesSafely(
                _paths.GetFolderPath(
                    game,
                    AssetFolder.ArtworkCustom),
                SearchOption.AllDirectories,
                issues);

        int activeArtworkCount =
            CountFilesSafely(
                _paths.GetFolderPath(
                    game,
                    AssetFolder.ArtworkActive),
                SearchOption.AllDirectories,
                issues);

        int screenshotCount =
            CountFilesSafely(
                _paths.GetFolderPath(
                    game,
                    AssetFolder.Screenshots),
                SearchOption.AllDirectories,
                issues);

        int videoCount =
            CountFilesSafely(
                _paths.GetFolderPath(
                    game,
                    AssetFolder.Videos),
                SearchOption.AllDirectories,
                issues);

        int saveBackupCount =
            CountSaveBackupsSafely(
                _paths.GetFolderPath(
                    game,
                    AssetFolder.Saves),
                issues);

        int modFileCount =
            CountFilesSafely(
                _paths.GetFolderPath(
                    game,
                    AssetFolder.Mods),
                SearchOption.AllDirectories,
                issues);

        int temporaryFileCount =
            CountFilesSafely(
                _paths.GetFolderPath(
                    game,
                    AssetFolder.Temp),
                SearchOption.AllDirectories,
                issues);

        if (temporaryFileCount > 0)
        {
            issues.Add(
                temporaryFileCount == 1
                    ? "The temporary asset folder contains one file."
                    : $"The temporary asset folder contains " +
                      $"{temporaryFileCount} files.");

            repairActions.Add(
                AssetRepairAction.ClearTemporaryFiles);
        }

        return new AssetCountInspection(
            OriginalArtworkCount: originalArtworkCount,
            GeneratedArtworkCount: generatedArtworkCount,
            CustomArtworkCount: customArtworkCount,
            ActiveArtworkCount: activeArtworkCount,
            ScreenshotCount: screenshotCount,
            VideoCount: videoCount,
            SaveBackupCount: saveBackupCount,
            ModFileCount: modFileCount);
    }

    /// <summary>
    /// Calculates total storage use and finds the newest managed file.
    /// </summary>
    private static StorageInspection InspectStorage(
        string gameRootPath,
        ICollection<string> issues,
        CancellationToken cancellationToken)
    {
        long totalSizeBytes = 0;
        DateTimeOffset? lastAssetActivityAt = null;

        try
        {
            foreach (
                string filePath in
                Directory.EnumerateFiles(
                    gameRootPath,
                    "*",
                    SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    FileInfo fileInfo =
                        new(filePath);

                    totalSizeBytes +=
                        fileInfo.Length;

                    DateTimeOffset modifiedAt =
                        new(
                            fileInfo.LastWriteTimeUtc,
                            TimeSpan.Zero);

                    if (lastAssetActivityAt is null ||
                        modifiedAt > lastAssetActivityAt.Value)
                    {
                        lastAssetActivityAt =
                            modifiedAt;
                    }
                }
                catch (Exception exception) when (
                    exception is IOException or
                    UnauthorizedAccessException or
                    FileNotFoundException)
                {
                    issues.Add(
                        $"A managed asset file could not be inspected: " +
                        $"'{filePath}'. {exception.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            DirectoryNotFoundException)
        {
            issues.Add(
                $"The asset directory could not be fully inspected: " +
                $"{exception.Message}");
        }

        return new StorageInspection(
            TotalSizeBytes: totalSizeBytes,
            LastAssetActivityAt: lastAssetActivityAt);
    }

    /// <summary>
    /// Counts all files inside a directory without allowing a filesystem
    /// error to terminate the complete game inspection.
    /// </summary>
    private static int CountFilesSafely(
        string folderPath,
        SearchOption searchOption,
        ICollection<string> issues)
    {
        if (!Directory.Exists(folderPath))
        {
            return 0;
        }

        try
        {
            return Directory
                .EnumerateFiles(
                    folderPath,
                    "*",
                    searchOption)
                .Count();
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            DirectoryNotFoundException)
        {
            issues.Add(
                $"Files in '{folderPath}' could not be counted: " +
                $"{exception.Message}");

            return 0;
        }
    }

    /// <summary>
    /// Counts save files while excluding the standard save metadata file.
    /// </summary>
    private static int CountSaveBackupsSafely(
        string savesFolderPath,
        ICollection<string> issues)
    {
        if (!Directory.Exists(savesFolderPath))
        {
            return 0;
        }

        try
        {
            return Directory
                .EnumerateFiles(
                    savesFolderPath,
                    "*",
                    SearchOption.AllDirectories)
                .Count(
                    filePath =>
                        !string.Equals(
                            Path.GetFileName(filePath),
                            "metadata.json",
                            StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            DirectoryNotFoundException)
        {
            issues.Add(
                $"Save backups in '{savesFolderPath}' could not be " +
                $"counted: {exception.Message}");

            return 0;
        }
    }

    private static string NormalizeInspectionGameName(
        string? gameName)
    {
        return string.IsNullOrWhiteSpace(gameName)
            ? "Unknown Game"
            : gameName.Trim();
    }

    private readonly record struct OwnershipInspection(
        bool HasMetadata,
        bool HasValidMetadata,
        bool HasManifest,
        bool HasValidManifest,
        AssetMetadata? Metadata);

    private readonly record struct ArtworkInspection(
        bool HasCover,
        bool HasHero,
        bool HasLogo,
        bool HasThumbnail,
        bool HasBackground,
        bool HasIcon);

    private readonly record struct AssetCountInspection(
        int OriginalArtworkCount,
        int GeneratedArtworkCount,
        int CustomArtworkCount,
        int ActiveArtworkCount,
        int ScreenshotCount,
        int VideoCount,
        int SaveBackupCount,
        int ModFileCount);

    private readonly record struct StorageInspection(
        long TotalSizeBytes,
        DateTimeOffset? LastAssetActivityAt);
}