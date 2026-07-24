using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NovaLauncher.Models;

namespace NovaLauncher.Services.Assets;

/// <summary>
/// Provides all filesystem paths used by NovaLauncher's asset system.
///
/// This class only resolves paths. It does not create, move,
/// modify, or delete files and directories. Filesystem operations
/// belong to <see cref="AssetManager"/>.
/// </summary>
public sealed class AssetPaths
{
    private const string ApplicationFolderName = "NovaLauncher";
    private const string AssetsFolderName = "Assets";

    private const string MetadataFileName = "metadata.json";
    private const string ManifestFileName = ".nova";

    private readonly string _assetsRoot;

    /// <summary>
    /// Creates an AssetPaths instance using NovaLauncher's default
    /// local application-data directory.
    ///
    /// Default:
    /// %LocalAppData%/NovaLauncher/Assets/
    /// </summary>
    public AssetPaths()
        : this(GetDefaultAssetsRoot())
    {
    }

    /// <summary>
    /// Creates an AssetPaths instance using a custom assets directory.
    ///
    /// This supports future features such as portable mode,
    /// user-selected storage locations, and external drives.
    /// </summary>
    /// <param name="assetsRoot">
    /// Root directory under which all game asset folders are stored.
    /// </param>
    public AssetPaths(string assetsRoot)
    {
        if (string.IsNullOrWhiteSpace(assetsRoot))
        {
            throw new ArgumentException(
                "The assets root path cannot be empty.",
                nameof(assetsRoot));
        }

        _assetsRoot = Path.GetFullPath(
            Environment.ExpandEnvironmentVariables(
                assetsRoot.Trim()));
    }

    /// <summary>
    /// Root directory containing assets for every game.
    ///
    /// Example:
    /// C:/Users/User/AppData/Local/NovaLauncher/Assets/
    /// </summary>
    public string AssetsRoot => _assetsRoot;

    /// <summary>
    /// Returns NovaLauncher's default assets directory.
    /// </summary>
    public static string GetDefaultAssetsRoot()
    {
        string localApplicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException(
                "The local application-data directory could not be resolved.");
        }

        return Path.Combine(
            localApplicationData,
            ApplicationFolderName,
            AssetsFolderName);
    }

    /// <summary>
    /// Gets the root asset directory for a game.
    ///
    /// The visible game name makes the directory understandable
    /// to humans, while the full game ID provides stable identity.
    ///
    /// Example:
    /// Cyberpunk 2077_3f28c2d70c734ed2a38f9b5adff62a25/
    /// </summary>
    public string GetGameRoot(Game game)
    {
        ValidateGame(game);

        string? existingGameRoot =
            FindExistingGameRoot(game.Id);

        if (!string.IsNullOrWhiteSpace(existingGameRoot))
        {
            return existingGameRoot;
        }

        string safeGameName =
            MakeSafePathSegment(game.Name);

        string gameFolderName =
            $"{safeGameName}_{game.Id:N}";

        return Path.Combine(
            AssetsRoot,
            gameFolderName);
    }

    /// <summary>
    /// Gets the directory represented by an AssetFolder value.
    /// </summary>
    public string GetFolderPath(
        Game game,
        AssetFolder folder)
    {
        string gameRoot = GetGameRoot(game);

        return folder switch
        {
            AssetFolder.GameRoot =>
                gameRoot,

            AssetFolder.Artwork =>
                Path.Combine(
                    gameRoot,
                    "Artwork"),

            AssetFolder.ArtworkOriginal =>
                Path.Combine(
                    gameRoot,
                    "Artwork",
                    "Original"),

            AssetFolder.ArtworkGenerated =>
                Path.Combine(
                    gameRoot,
                    "Artwork",
                    "Generated"),

            AssetFolder.ArtworkCustom =>
                Path.Combine(
                    gameRoot,
                    "Artwork",
                    "Custom"),

            AssetFolder.ArtworkActive =>
                Path.Combine(
                    gameRoot,
                    "Artwork",
                    "Active"),

            AssetFolder.Saves =>
                Path.Combine(
                    gameRoot,
                    "Saves"),

            AssetFolder.Screenshots =>
                Path.Combine(
                    gameRoot,
                    "Screenshots"),

            AssetFolder.Videos =>
                Path.Combine(
                    gameRoot,
                    "Videos"),

            AssetFolder.Icons =>
                Path.Combine(
                    gameRoot,
                    "Icons"),

            AssetFolder.Mods =>
                Path.Combine(
                    gameRoot,
                    "Mods"),

            AssetFolder.Cache =>
                Path.Combine(
                    gameRoot,
                    "Cache"),

            AssetFolder.Temp =>
                Path.Combine(
                    gameRoot,
                    "Temp"),

            _ => throw new ArgumentOutOfRangeException(
                nameof(folder),
                folder,
                "Unknown asset folder.")
        };
    }

    /// <summary>
    /// Gets the game's asset metadata file.
    ///
    /// Example:
    /// Assets/GameName_GameId/metadata.json
    /// </summary>
    public string GetMetadataPath(Game game)
    {
        return Path.Combine(
            GetGameRoot(game),
            MetadataFileName);
    }

    /// <summary>
    /// Gets the internal NovaLauncher folder manifest.
    ///
    /// Example:
    /// Assets/GameName_GameId/.nova
    /// </summary>
    public string GetManifestPath(Game game)
    {
        return Path.Combine(
            GetGameRoot(game),
            ManifestFileName);
    }

    /// <summary>
    /// Gets the standard active artwork path for a game.
    ///
    /// Examples:
    /// Artwork/Active/cover.webp
    /// Artwork/Active/hero.webp
    /// Artwork/Active/logo.webp
    /// </summary>
    public string GetActiveArtworkPath(
        Game game,
        AssetType assetType)
    {
        EnsureArtworkType(assetType);

        return GetAssetPath(
            game,
            AssetFolder.ArtworkActive,
            assetType);
    }

    /// <summary>
    /// Gets the standard generated artwork path for a game.
    ///
    /// Examples:
    /// Artwork/Generated/cover.webp
    /// Artwork/Generated/thumbnail.webp
    /// Artwork/Generated/background.webp
    /// </summary>
    public string GetGeneratedArtworkPath(
        Game game,
        AssetType assetType)
    {
        EnsureArtworkType(assetType);

        return GetAssetPath(
            game,
            AssetFolder.ArtworkGenerated,
            assetType);
    }

    /// <summary>
    /// Gets a standard singleton asset path.
    ///
    /// This method is intended for assets that have one predictable
    /// filename, such as cover.webp or icon.webp.
    ///
    /// Collection assets such as screenshots, videos, save backups,
    /// and custom artwork must provide their own unique filename
    /// through GetNamedAssetPath.
    /// </summary>
    public string GetAssetPath(
        Game game,
        AssetFolder folder,
        AssetType assetType)
    {
        string fileName =
            GetStandardFileName(assetType);

        ValidateFolderForAssetType(
            folder,
            assetType);

        return Path.Combine(
            GetFolderPath(game, folder),
            fileName);
    }

    /// <summary>
    /// Gets a path for an asset that requires a caller-provided
    /// filename, such as a screenshot, video, provider original,
    /// save backup, or custom cover.
    ///
    /// The filename is sanitized before being added to the path.
    /// </summary>
    public string GetNamedAssetPath(
        Game game,
        AssetFolder folder,
        string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException(
                "The asset filename cannot be empty.",
                nameof(fileName));
        }

        string safeFileName =
            MakeSafeFileName(fileName.Trim());

        return Path.Combine(
            GetFolderPath(game, folder),
            safeFileName);
    }

    /// <summary>
    /// Returns the conventional filename for a singleton asset type.
    /// </summary>
    public static string GetStandardFileName(
        AssetType assetType)
    {
        return assetType switch
        {
            AssetType.Cover =>
                "cover.webp",

            AssetType.Hero =>
                "hero.webp",

            AssetType.Logo =>
                "logo.webp",

            AssetType.Thumbnail =>
                "thumbnail.webp",

            AssetType.Background =>
                "background.webp",

            AssetType.Icon =>
                "icon.webp",

            AssetType.LargeIcon =>
                "icon-large.webp",

            AssetType.SaveMetadata =>
                "metadata.json",

            AssetType.ModManifest =>
                "manifest.json",

            AssetType.Metadata =>
                MetadataFileName,

            AssetType.Manifest =>
                ManifestFileName,

            AssetType.OriginalArtwork or
            AssetType.SaveBackup or
            AssetType.Screenshot or
            AssetType.Video or
            AssetType.ModThumbnail or
            AssetType.Cache or
            AssetType.Temporary =>
                throw new InvalidOperationException(
                    $"{assetType} does not have one standard filename. " +
                    "Use GetNamedAssetPath instead."),

            _ => throw new ArgumentOutOfRangeException(
                nameof(assetType),
                assetType,
                "Unknown asset type.")
        };
    }

    /// <summary>
    /// Converts an arbitrary value into a safe directory-name segment.
    /// </summary>
    public static string MakeSafePathSegment(
        string? value)
    {
        string safeValue =
            string.IsNullOrWhiteSpace(value)
                ? "Unknown Game"
                : value.Trim();

        foreach (char invalidCharacter
                 in Path.GetInvalidFileNameChars())
        {
            safeValue =
                safeValue.Replace(
                    invalidCharacter,
                    '_');
        }

        safeValue =
            safeValue.Trim(
                ' ',
                '.');

        while (safeValue.Contains(
                   "  ",
                   StringComparison.Ordinal))
        {
            safeValue =
                safeValue.Replace(
                    "  ",
                    " ",
                    StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(safeValue)
            ? "Unknown Game"
            : safeValue;
    }

    /// <summary>
    /// Converts a supplied filename into a filesystem-safe filename
    /// while preserving its extension.
    /// </summary>
    public static string MakeSafeFileName(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "The filename cannot be empty.",
                nameof(value));
        }

        string safeValue = value.Trim();

        foreach (char invalidCharacter
                 in Path.GetInvalidFileNameChars())
        {
            safeValue =
                safeValue.Replace(
                    invalidCharacter,
                    '_');
        }

        safeValue =
            safeValue.Trim(
                ' ',
                '.');

        if (string.IsNullOrWhiteSpace(safeValue))
        {
            throw new ArgumentException(
                "The filename contains no usable characters.",
                nameof(value));
        }

        return safeValue;
    }

    /// <summary>
    /// Finds an existing game directory by its stable game ID.
    ///
    /// This prevents a renamed game from receiving a second asset
    /// folder merely because its display name changed.
    /// </summary>
    private string? FindExistingGameRoot(
        Guid gameId)
    {
        if (!Directory.Exists(AssetsRoot))
        {
            return null;
        }

        string expectedSuffix =
            $"_{gameId:N}";

        try
        {
            return Directory
                .EnumerateDirectories(
                    AssetsRoot,
                    $"*{expectedSuffix}",
                    SearchOption.TopDirectoryOnly)
                .FirstOrDefault(
                    directory =>
                        Path.GetFileName(directory)
                            .EndsWith(
                                expectedSuffix,
                                StringComparison.OrdinalIgnoreCase));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void ValidateGame(
        Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (game.Id == Guid.Empty)
        {
            throw new InvalidOperationException(
                "A game must have a valid ID before assets can be resolved.");
        }
    }

    private static void EnsureArtworkType(
        AssetType assetType)
    {
        if (!IsArtworkType(assetType))
        {
            throw new ArgumentException(
                $"{assetType} is not an artwork asset type.",
                nameof(assetType));
        }
    }

    private static bool IsArtworkType(
        AssetType assetType)
    {
        return assetType is
            AssetType.Cover or
            AssetType.Hero or
            AssetType.Logo or
            AssetType.Thumbnail or
            AssetType.Background;
    }

    private static void ValidateFolderForAssetType(
        AssetFolder folder,
        AssetType assetType)
    {
        if (assetType == AssetType.Metadata &&
            folder != AssetFolder.GameRoot)
        {
            throw new ArgumentException(
                "Asset metadata must be stored in the game root.",
                nameof(folder));
        }

        if (assetType == AssetType.Manifest &&
            folder != AssetFolder.GameRoot)
        {
            throw new ArgumentException(
                "The Nova manifest must be stored in the game root.",
                nameof(folder));
        }

        if (assetType is AssetType.Icon or AssetType.LargeIcon &&
            folder != AssetFolder.Icons)
        {
            throw new ArgumentException(
                "Icon assets must be stored in the Icons folder.",
                nameof(folder));
        }

        if (assetType == AssetType.SaveMetadata &&
            folder != AssetFolder.Saves)
        {
            throw new ArgumentException(
                "Save metadata must be stored in the Saves folder.",
                nameof(folder));
        }

        if (assetType == AssetType.ModManifest &&
            folder != AssetFolder.Mods)
        {
            throw new ArgumentException(
                "Mod manifests must be stored in the Mods folder.",
                nameof(folder));
        }

        if (IsArtworkType(assetType) &&
            folder is not (
                AssetFolder.ArtworkOriginal or
                AssetFolder.ArtworkGenerated or
                AssetFolder.ArtworkCustom or
                AssetFolder.ArtworkActive))
        {
            throw new ArgumentException(
                "Artwork assets must be stored in an artwork folder.",
                nameof(folder));
        }
    }
}