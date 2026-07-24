using System;
using System.Collections.Generic;

namespace NovaLauncher.Services.Assets;

/// <summary>
/// Represents a snapshot of the managed asset state for one game.
///
/// GameAssetInfo does not perform filesystem operations.
/// It is created and populated by AssetManager after inspecting
/// a game's managed asset directory.
/// </summary>
public sealed class GameAssetState
{
    /// <summary>
    /// Stable NovaLauncher identifier for the game.
    /// </summary>
    public Guid GameId { get; init; }

    /// <summary>
    /// Human-readable name of the game.
    /// </summary>
    public string GameName { get; init; } =
        string.Empty;

    /// <summary>
    /// Absolute path to the game's managed asset root.
    /// </summary>
    public string GameRootPath { get; init; } =
        string.Empty;

    /// <summary>
    /// Indicates whether the game's root asset directory exists.
    /// </summary>
    public bool HasGameRoot { get; init; }

    /// <summary>
    /// Indicates whether every required managed directory exists.
    /// </summary>
    public bool HasRequiredFolders { get; init; }

    /// <summary>
    /// Indicates whether metadata.json exists.
    /// </summary>
    public bool HasMetadata { get; init; }

    /// <summary>
    /// Indicates whether metadata.json was successfully loaded
    /// and validated.
    /// </summary>
    public bool HasValidMetadata { get; init; }

    /// <summary>
    /// Indicates whether the .nova manifest exists.
    /// </summary>
    public bool HasManifest { get; init; }

    /// <summary>
    /// Indicates whether the .nova manifest belongs to this game
    /// and uses a supported format.
    /// </summary>
    public bool HasValidManifest { get; init; }

    /// <summary>
    /// Indicates whether an active portrait cover exists.
    /// </summary>
    public bool HasCover { get; init; }

    /// <summary>
    /// Indicates whether an active wide hero image exists.
    /// </summary>
    public bool HasHero { get; init; }

    /// <summary>
    /// Indicates whether an active transparent logo exists.
    /// </summary>
    public bool HasLogo { get; init; }

    /// <summary>
    /// Indicates whether an active optimized thumbnail exists.
    /// </summary>
    public bool HasThumbnail { get; init; }

    /// <summary>
    /// Indicates whether an active processed background exists.
    /// </summary>
    public bool HasBackground { get; init; }

    /// <summary>
    /// Indicates whether an active game icon exists.
    /// </summary>
    public bool HasIcon { get; init; }

    /// <summary>
    /// Number of original artwork files retained from providers
    /// or imports.
    /// </summary>
    public int OriginalArtworkCount { get; init; }

    /// <summary>
    /// Number of generated artwork files.
    /// </summary>
    public int GeneratedArtworkCount { get; init; }

    /// <summary>
    /// Number of custom artwork files supplied by the user.
    /// </summary>
    public int CustomArtworkCount { get; init; }

    /// <summary>
    /// Number of files currently stored in Artwork/Active.
    /// </summary>
    public int ActiveArtworkCount { get; init; }

    /// <summary>
    /// Number of managed screenshots.
    /// </summary>
    public int ScreenshotCount { get; init; }

    /// <summary>
    /// Number of managed videos.
    /// </summary>
    public int VideoCount { get; init; }

    /// <summary>
    /// Number of managed save backups.
    /// </summary>
    public int SaveBackupCount { get; init; }

    /// <summary>
    /// Number of files associated with managed mods.
    /// </summary>
    public int ModFileCount { get; init; }

    /// <summary>
    /// Total size of all managed files for this game, in bytes.
    /// </summary>
    public long TotalSizeBytes { get; init; }

    /// <summary>
    /// Date and time when the newest managed asset was modified.
    /// </summary>
    public DateTimeOffset? LastAssetActivityAt { get; init; }

    /// <summary>
    /// Asset metadata loaded during inspection, when valid.
    /// </summary>
    public AssetMetadata? Metadata { get; init; }

    /// <summary>
    /// Problems found while inspecting the game's assets.
    ///
    /// Examples:
    /// Missing metadata
    /// Missing hero artwork
    /// Invalid manifest
    /// Unreadable directory
    /// </summary>
    public IReadOnlyList<string> Issues { get; init; } =
        Array.Empty<string>();

    /// <summary>
    /// Recommended repair actions for the game's assets.
    /// </summary>
    public IReadOnlyList<AssetRepairAction> RepairActions { get; init; } =
        Array.Empty<AssetRepairAction>();

    /// <summary>
    /// Indicates whether the asset directory is completely initialized.
    /// </summary>
    public bool IsInitialized =>
        HasGameRoot &&
        HasRequiredFolders &&
        HasMetadata &&
        HasManifest;

    /// <summary>
    /// Indicates whether the asset directory appears structurally
    /// healthy and its ownership files are valid.
    /// </summary>
    public bool IsStructurallyHealthy =>
        HasGameRoot &&
        HasRequiredFolders &&
        HasValidMetadata &&
        HasValidManifest;

    /// <summary>
    /// Indicates whether the game has the minimum artwork needed
    /// for a polished library and details-page experience.
    ///
    /// Logo and background are considered optional because some
    /// providers may not supply them.
    /// </summary>
    public bool HasEssentialArtwork =>
        HasCover &&
        HasHero &&
        HasThumbnail;

    /// <summary>
    /// Indicates whether any recoverable problems were discovered.
    /// </summary>
    public bool NeedsRepair =>
        RepairActions.Count > 0;

    /// <summary>
    /// A score from 0 to 100 representing the completeness
    /// and health of the game's managed assets.
    ///
    /// Structural health contributes 40 points.
    /// Essential artwork contributes 45 points.
    /// Optional artwork contributes 15 points.
    /// </summary>
    public int HealthScore
    {
        get
        {
            int score = 0;

            if (HasGameRoot)
            {
                score += 5;
            }

            if (HasRequiredFolders)
            {
                score += 10;
            }

            if (HasValidMetadata)
            {
                score += 15;
            }

            if (HasValidManifest)
            {
                score += 10;
            }

            if (HasCover)
            {
                score += 20;
            }

            if (HasHero)
            {
                score += 15;
            }

            if (HasThumbnail)
            {
                score += 10;
            }

            if (HasLogo)
            {
                score += 5;
            }

            if (HasBackground)
            {
                score += 5;
            }

            if (HasIcon)
            {
                score += 5;
            }

            return Math.Clamp(
                score,
                0,
                100);
        }
    }

    /// <summary>
    /// Friendly label describing the current health score.
    /// </summary>
    public string HealthStatus =>
        HealthScore switch
        {
            >= 100 =>
                "Complete",

            >= 85 =>
                "Excellent",

            >= 70 =>
                "Good",

            >= 50 =>
                "Needs Attention",

            >= 25 =>
                "Incomplete",

            _ =>
                "Not Initialized"
        };

    /// <summary>
    /// Human-readable representation of the managed asset size.
    /// </summary>
    public string FormattedTotalSize =>
        FormatFileSize(TotalSizeBytes);

    /// <summary>
    /// Creates an empty result for a game whose assets have not
    /// been initialized.
    /// </summary>
    public static GameAssetState CreateUninitialized(
        Guid gameId,
        string? gameName,
        string? gameRootPath = null)
    {
        return new GameAssetState
        {
            GameId =
                gameId,

            GameName =
                NormalizeGameName(gameName),

            GameRootPath =
                gameRootPath ?? string.Empty,

            HasGameRoot =
                false,

            HasRequiredFolders =
                false,

            HasMetadata =
                false,

            HasValidMetadata =
                false,

            HasManifest =
                false,

            HasValidManifest =
                false,

            Issues =
            [
                "The game asset directory has not been initialized."
            ],

            RepairActions =
            [
                AssetRepairAction.InitializeAssetStructure,
                AssetRepairAction.CreateManifest,
                AssetRepairAction.CreateMetadata
            ]
        };
    }

    private static string NormalizeGameName(
        string? gameName)
    {
        return string.IsNullOrWhiteSpace(gameName)
            ? "Unknown Game"
            : gameName.Trim();
    }

    private static string FormatFileSize(
        long bytes)
    {
        if (bytes < 0)
        {
            return "0 B";
        }

        string[] units =
        [
            "B",
            "KB",
            "MB",
            "GB",
            "TB"
        ];

        double size = bytes;
        int unitIndex = 0;

        while (size >= 1024 &&
               unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{size:0} {units[unitIndex]}"
            : $"{size:0.##} {units[unitIndex]}";
    }
}