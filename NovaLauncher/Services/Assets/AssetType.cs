namespace NovaLauncher.Services.Assets;

/// <summary>
/// Represents a managed asset within NovaLauncher.
///
/// Asset types are independent of where the file is stored.
/// AssetPaths determines the physical location, while AssetType
/// identifies what the file represents.
/// </summary>
public enum AssetType
{
    // Artwork
    Cover,
    Hero,
    Logo,
    Thumbnail,
    Background,
    OriginalArtwork,

    // Icons
    Icon,
    LargeIcon,

    // Metadata
    Metadata,
    Manifest,

    // Saves
    SaveMetadata,
    SaveBackup,

    // Media
    Screenshot,
    Video,

    // Mods
    ModManifest,
    ModThumbnail,

    // Cache
    Cache,
    Temporary
}