namespace NovaLauncher.Services.Assets;

/// <summary>
/// Describes an action that may be used to repair or improve
/// the managed assets belonging to a game.
/// </summary>
public enum AssetRepairAction
{
    /// <summary>
    /// No repair action is required.
    /// </summary>
    None,

    #region Structure

    /// <summary>
    /// Create the complete managed asset structure for the game.
    /// </summary>
    InitializeAssetStructure,

    /// <summary>
    /// Create one or more missing asset directories.
    /// </summary>
    CreateMissingFolders,

    #endregion

    #region Manifest

    /// <summary>
    /// Create the game's missing .nova manifest.
    /// </summary>
    CreateManifest,

    /// <summary>
    /// Replace or repair an invalid .nova manifest.
    /// </summary>
    RepairManifest,

    #endregion

    #region Metadata

    /// <summary>
    /// Create the game's missing metadata.json file.
    /// </summary>
    CreateMetadata,

    /// <summary>
    /// Replace or repair invalid asset metadata.
    /// </summary>
    RepairMetadata,

    /// <summary>
    /// Rebuild metadata records from files found on disk.
    /// </summary>
    RebuildMetadataIndex,

    #endregion

    #region Artwork

    /// <summary>
    /// Download or import cover artwork.
    /// </summary>
    AcquireCover,

    /// <summary>
    /// Download or import hero artwork.
    /// </summary>
    AcquireHero,

    /// <summary>
    /// Download or import logo artwork.
    /// </summary>
    AcquireLogo,

    /// <summary>
    /// Generate a thumbnail from existing artwork.
    /// </summary>
    GenerateThumbnail,

    /// <summary>
    /// Generate a background from existing artwork.
    /// </summary>
    GenerateBackground,

    /// <summary>
    /// Download, import, or generate a game icon.
    /// </summary>
    AcquireIcon,

    #endregion

    #region Cleanup

    /// <summary>
    /// Remove files from the game's temporary directory.
    /// </summary>
    ClearTemporaryFiles,

    /// <summary>
    /// Remove invalid or orphaned cache files.
    /// </summary>
    ClearInvalidCache

    #endregion
}