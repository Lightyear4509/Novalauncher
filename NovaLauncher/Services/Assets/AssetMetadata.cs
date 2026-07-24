using System;
using System.Collections.Generic;

namespace NovaLauncher.Services.Assets;

/// <summary>
/// Represents persistent metadata for a game's managed assets.
///
/// This file is intended to be serialized as:
/// Assets/GameName_GameId/metadata.json
///
/// AssetMetadata describes the contents and state of the asset folder.
/// It does not contain the game's general library metadata.
/// </summary>
public sealed class AssetMetadata
{
    /// <summary>
    /// Current metadata schema version.
    ///
    /// Increment this value whenever a breaking change is made
    /// to the metadata structure.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Version of the metadata schema used by this file.
    /// </summary>
    public int SchemaVersion { get; set; } =
        CurrentSchemaVersion;

    /// <summary>
    /// Stable NovaLauncher identifier for the game.
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Human-readable game name stored for diagnostics,
    /// migration, and asset-folder recovery.
    ///
    /// The game ID remains the authoritative identity.
    /// </summary>
    public string GameName { get; set; } =
        string.Empty;

    /// <summary>
    /// Date and time when this metadata file was first created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } =
        DateTimeOffset.UtcNow;

    /// <summary>
    /// Date and time when this metadata was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } =
        DateTimeOffset.UtcNow;

    /// <summary>
    /// Artwork-related asset metadata.
    /// </summary>
    public ArtworkAssetMetadata Artwork { get; set; } =
        new();

    /// <summary>
    /// Save-related asset metadata.
    /// </summary>
    public SaveAssetMetadata Saves { get; set; } =
        new();

    /// <summary>
    /// Screenshot-related asset metadata.
    /// </summary>
    public ScreenshotAssetMetadata Screenshots { get; set; } =
        new();

    /// <summary>
    /// Video-related asset metadata.
    /// </summary>
    public VideoAssetMetadata Videos { get; set; } =
        new();

    /// <summary>
    /// Mod-related asset metadata.
    /// </summary>
    public ModAssetMetadata Mods { get; set; } =
        new();

    /// <summary>
    /// Additional metadata supplied by future providers,
    /// plugins, or migration systems.
    ///
    /// This allows the schema to grow without requiring every
    /// extension to modify the core AssetMetadata class.
    /// </summary>
    public Dictionary<string, string> Extensions { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a new metadata object for a game.
    /// </summary>
    public static AssetMetadata Create(
        Guid gameId,
        string? gameName)
    {
        if (gameId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid game ID is required.",
                nameof(gameId));
        }

        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        return new AssetMetadata
        {
            SchemaVersion =
                CurrentSchemaVersion,

            GameId =
                gameId,

            GameName =
                string.IsNullOrWhiteSpace(gameName)
                    ? "Unknown Game"
                    : gameName.Trim(),

            CreatedAt =
                now,

            UpdatedAt =
                now
        };
    }

    /// <summary>
    /// Updates the metadata's modification timestamp.
    /// </summary>
    public void Touch()
    {
        UpdatedAt =
            DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Metadata describing artwork managed for a game.
/// </summary>
public sealed class ArtworkAssetMetadata
{
    /// <summary>
    /// Provider or source currently supplying the active cover.
    ///
    /// Examples:
    /// Steam
    /// SteamGridDB
    /// Custom
    /// Generated
    /// </summary>
    public string? ActiveCoverSource { get; set; }

    /// <summary>
    /// Provider or source currently supplying the active hero.
    /// </summary>
    public string? ActiveHeroSource { get; set; }

    /// <summary>
    /// Provider or source currently supplying the active logo.
    /// </summary>
    public string? ActiveLogoSource { get; set; }

    /// <summary>
    /// Relative path to the active cover inside the game's
    /// managed asset directory.
    ///
    /// Example:
    /// Artwork/Active/cover.webp
    /// </summary>
    public string? ActiveCoverPath { get; set; }

    /// <summary>
    /// Relative path to the active hero inside the game's
    /// managed asset directory.
    /// </summary>
    public string? ActiveHeroPath { get; set; }

    /// <summary>
    /// Relative path to the active logo inside the game's
    /// managed asset directory.
    /// </summary>
    public string? ActiveLogoPath { get; set; }

    /// <summary>
    /// Relative path to the active thumbnail.
    /// </summary>
    public string? ActiveThumbnailPath { get; set; }

    /// <summary>
    /// Relative path to the active background artwork.
    /// </summary>
    public string? ActiveBackgroundPath { get; set; }

    /// <summary>
    /// All providers or sources that have contributed artwork.
    /// </summary>
    public List<string> Sources { get; set; } =
        [];

    /// <summary>
    /// Detailed records for every managed artwork file.
    /// </summary>
    public List<ArtworkAssetRecord> Items { get; set; } =
        [];
}

/// <summary>
/// Describes one artwork file stored for a game.
/// </summary>
public sealed class ArtworkAssetRecord
{
    /// <summary>
    /// Unique identifier for this asset record.
    /// </summary>
    public Guid Id { get; set; } =
        Guid.NewGuid();

    /// <summary>
    /// Type of artwork represented by this record.
    /// </summary>
    public AssetType Type { get; set; }

    /// <summary>
    /// Source that supplied the artwork.
    ///
    /// Examples:
    /// Steam
    /// SteamGridDB
    /// Epic
    /// Custom
    /// Generated
    /// </summary>
    public string Source { get; set; } =
        string.Empty;

    /// <summary>
    /// Relative path to the stored asset.
    /// </summary>
    public string RelativePath { get; set; } =
        string.Empty;

    /// <summary>
    /// Original remote or imported source location.
    ///
    /// This may be a URL, local path, or provider-specific value.
    /// </summary>
    public string? SourceLocation { get; set; }

    /// <summary>
    /// Width of the image in pixels, when known.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Height of the image in pixels, when known.
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Size of the stored file in bytes, when known.
    /// </summary>
    public long? FileSizeBytes { get; set; }

    /// <summary>
    /// File hash used to detect duplicates or corruption.
    ///
    /// The hashing algorithm should be recorded in HashAlgorithm.
    /// </summary>
    public string? FileHash { get; set; }

    /// <summary>
    /// Name of the algorithm used by FileHash.
    ///
    /// Example:
    /// SHA256
    /// </summary>
    public string? HashAlgorithm { get; set; }

    /// <summary>
    /// Date and time when the artwork was added.
    /// </summary>
    public DateTimeOffset AddedAt { get; set; } =
        DateTimeOffset.UtcNow;

    /// <summary>
    /// Indicates whether this artwork was manually supplied
    /// by the user.
    /// </summary>
    public bool IsCustom { get; set; }

    /// <summary>
    /// Indicates whether this artwork is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Indicates whether NovaLauncher generated this asset
    /// from another source image.
    /// </summary>
    public bool IsGenerated { get; set; }

    /// <summary>
    /// Optional identifier of the source artwork record used
    /// to generate this asset.
    /// </summary>
    public Guid? GeneratedFromAssetId { get; set; }
}

/// <summary>
/// Metadata describing save-related assets.
/// </summary>
public sealed class SaveAssetMetadata
{
    /// <summary>
    /// Configured game save directory, when known.
    /// </summary>
    public string? SaveDirectory { get; set; }

    /// <summary>
    /// Date and time when save activity was last detected.
    /// </summary>
    public DateTimeOffset? LastActivityAt { get; set; }

    /// <summary>
    /// Date and time when saves were last synchronized.
    /// </summary>
    public DateTimeOffset? LastSynchronizedAt { get; set; }

    /// <summary>
    /// Number of managed save backups.
    /// </summary>
    public int BackupCount { get; set; }

    /// <summary>
    /// Whether save synchronization is enabled for this game.
    /// </summary>
    public bool SynchronizationEnabled { get; set; }

    /// <summary>
    /// Save synchronization provider or strategy.
    ///
    /// Examples:
    /// Local
    /// Tailscale
    /// NAS
    /// Plugin
    /// </summary>
    public string? SynchronizationProvider { get; set; }
}

/// <summary>
/// Metadata describing managed screenshots.
/// </summary>
public sealed class ScreenshotAssetMetadata
{
    /// <summary>
    /// Number of screenshots currently managed.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Date and time when the newest screenshot was added.
    /// </summary>
    public DateTimeOffset? LastAddedAt { get; set; }
}

/// <summary>
/// Metadata describing managed videos.
/// </summary>
public sealed class VideoAssetMetadata
{
    /// <summary>
    /// Number of videos currently managed.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Relative path to the selected preview or trailer.
    /// </summary>
    public string? ActivePreviewPath { get; set; }
}

/// <summary>
/// Metadata describing managed mods.
/// </summary>
public sealed class ModAssetMetadata
{
    /// <summary>
    /// Number of mods currently known to NovaLauncher.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Date and time when mod metadata was last refreshed.
    /// </summary>
    public DateTimeOffset? LastRefreshedAt { get; set; }

    /// <summary>
    /// Mod provider or integration currently being used.
    ///
    /// Examples:
    /// NexusMods
    /// CurseForge
    /// Local
    /// Plugin
    /// </summary>
    public string? Provider { get; set; }
}