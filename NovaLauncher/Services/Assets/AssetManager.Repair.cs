using NovaLauncher.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NovaLauncher.Services.Assets;

/// <summary>
/// Provides repair functionality for AssetManager.
///
/// Repair operations are driven entirely by the results of
/// InspectGameAsync(). The repair engine never decides what
/// needs repairing—it only executes recommended actions.
/// </summary>
public sealed partial class AssetManager
{
    /// <summary>
    /// Repairs the managed assets for a game and returns a fresh
    /// inspection after all requested repairs have completed.
    /// </summary>
    public async Task<GameAssetState> RepairGameAsync(
        Game game,
        CancellationToken cancellationToken = default)
    {
        ValidateGame(game);

        GameAssetState inspection =
            await InspectGameAsync(
                game,
                cancellationToken);

        foreach (AssetRepairAction action in inspection.RepairActions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await ExecuteRepairAsync(
                game,
                action,
                cancellationToken);
        }

        return await InspectGameAsync(
            game,
            cancellationToken);
    }

    /// <summary>
    /// Repairs multiple games sequentially.
    /// </summary>
    public async Task<IReadOnlyList<GameAssetState>>
        RepairGamesAsync(
            IEnumerable<Game> games,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(games);

        List<GameAssetState> results = [];

        foreach (Game game in games)
        {
            cancellationToken.ThrowIfCancellationRequested();

            results.Add(
                await RepairGameAsync(
                    game,
                    cancellationToken));
        }

        return results;
    }

    /// <summary>
    /// Executes one repair action.
    /// </summary>
    private async Task ExecuteRepairAsync(
        Game game,
        AssetRepairAction repairAction,
        CancellationToken cancellationToken)
    {
        switch (repairAction)
        {
            case AssetRepairAction.InitializeAssetStructure:
                await RepairInitializeStructureAsync(
                    game,
                    cancellationToken);
                break;

            case AssetRepairAction.CreateMissingFolders:
                await RepairMissingFoldersAsync(
                    game,
                    cancellationToken);
                break;

            case AssetRepairAction.CreateManifest:
                await RepairManifestAsync(
                    game,
                    cancellationToken);
                break;

            case AssetRepairAction.RepairManifest:
                await RepairManifestAsync(
                    game,
                    cancellationToken);
                break;

            case AssetRepairAction.CreateMetadata:
                await RepairMetadataAsync(
                    game,
                    cancellationToken);
                break;

            case AssetRepairAction.RepairMetadata:
                await RepairMetadataAsync(
                    game,
                    cancellationToken);
                break;

            case AssetRepairAction.RebuildMetadataIndex:
                await RepairMetadataIndexAsync(
                    game,
                    cancellationToken);
                break;

            case AssetRepairAction.AcquireCover:
                await RepairCoverAsync(
                    game,
                    cancellationToken);
                break;

            case AssetRepairAction.AcquireHero:
                await RepairHeroAsync(
                    game,
                    cancellationToken);
                break;

            case AssetRepairAction.AcquireLogo:
                await RepairLogoAsync(
                    game,
                    cancellationToken);
                break;

            case AssetRepairAction.GenerateThumbnail:
                await RepairThumbnailAsync(
                    game,
                    cancellationToken);
                break;

            case AssetRepairAction.GenerateBackground:
                await RepairBackgroundAsync(
                    game,
                    cancellationToken);
                break;

            case AssetRepairAction.AcquireIcon:
                await RepairIconAsync(
                    game,
                    cancellationToken);
                break;

            case AssetRepairAction.ClearTemporaryFiles:
                RepairTemporaryFiles(game);
                break;

            case AssetRepairAction.ClearInvalidCache:
                RepairCache(game);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(repairAction),
                    repairAction,
                    "Unknown repair action.");
        }
    }

    private async Task RepairInitializeStructureAsync(
    Game game,
    CancellationToken cancellationToken)
    {
        await InitializeGameAsync(
            game,
            cancellationToken);
    }

    private async Task RepairMissingFoldersAsync(
    Game game,
    CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (AssetFolder folder in RequiredGameFolders)
        {
            string path =
                _paths.GetFolderPath(
                    game,
                    folder);

            Directory.CreateDirectory(path);
        }

        await Task.CompletedTask;
    }

    private async Task RepairManifestAsync(
    Game game,
    CancellationToken cancellationToken)
    {
        await EnsureManifestAsync(
            game,
            cancellationToken);
    }

    private async Task RepairMetadataAsync(
    Game game,
    CancellationToken cancellationToken)
    {
        AssetMetadata metadata =
            await LoadOrCreateMetadataAsync(
                game,
                cancellationToken);

        metadata.UpdatedAt =
            DateTimeOffset.UtcNow;

        await SaveMetadataAsync(
            game,
            metadata,
            cancellationToken);
    }

    private async Task RepairMetadataIndexAsync(
    Game game,
    CancellationToken cancellationToken)
    {
        await RebuildMetadataAsync(
            game,
            cancellationToken);
    }

    private Task RepairCoverAsync(
        Game game,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    private Task RepairHeroAsync(
        Game game,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    private Task RepairLogoAsync(
        Game game,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    private Task RepairThumbnailAsync(
        Game game,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    private Task RepairBackgroundAsync(
        Game game,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    private Task RepairIconAsync(
        Game game,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Removes all temporary asset files while preserving the Temp folder.
    /// </summary>
    private void RepairTemporaryFiles(Game game)
    {
        string temporaryFolderPath =
            _paths.GetFolderPath(
                game,
                AssetFolder.Temp);

        ClearFolderContents(temporaryFolderPath);
    }

    /// <summary>
    /// Removes cached asset files while preserving the Cache folder.
    /// </summary>
    private void RepairCache(Game game)
    {
        string cacheFolderPath =
            _paths.GetFolderPath(
                game,
                AssetFolder.Cache);

        ClearFolderContents(cacheFolderPath);
    }

    /// <summary>
    /// Deletes every file and subdirectory inside a managed folder while
    /// preserving the managed folder itself.
    /// </summary>
    private static void ClearFolderContents(string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        if (!Directory.Exists(folderPath))
        {
            return;
        }

        foreach (string filePath in Directory.EnumerateFiles(folderPath))
        {
            NormalizeFileAttributes(filePath);
            File.Delete(filePath);
        }

        foreach (string directoryPath in Directory.EnumerateDirectories(folderPath))
        {
            NormalizeDirectoryFileAttributes(directoryPath);
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    /// <summary>
    /// Removes attributes such as ReadOnly before deleting a file.
    /// </summary>
    private static void NormalizeFileAttributes(string filePath)
    {
        FileAttributes attributes =
            File.GetAttributes(filePath);

        if ((attributes & FileAttributes.ReadOnly) == 0)
        {
            return;
        }

        File.SetAttributes(
            filePath,
            attributes & ~FileAttributes.ReadOnly);
    }

    /// <summary>
    /// Removes ReadOnly attributes from files contained in a directory tree.
    /// </summary>
    private static void NormalizeDirectoryFileAttributes(
        string directoryPath)
    {
        foreach (
            string filePath in
            Directory.EnumerateFiles(
                directoryPath,
                "*",
                SearchOption.AllDirectories))
        {
            NormalizeFileAttributes(filePath);
        }
    }
}