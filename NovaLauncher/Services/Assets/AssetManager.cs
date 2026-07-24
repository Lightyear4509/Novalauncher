using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using NovaLauncher.Models;

namespace NovaLauncher.Services.Assets;

/// <summary>
/// Manages NovaLauncher's game asset directories, files,
/// manifests, and persistent asset metadata.
///
/// AssetManager owns filesystem operations such as:
/// - Creating game asset directories
/// - Locating managed assets
/// - Importing and activating files
/// - Reading and writing metadata
/// - Creating and validating Nova manifests
/// - Removing temporary or cached files
///
/// Path construction remains the responsibility of
/// <see cref="AssetPaths"/>.
/// </summary>
public sealed partial class AssetManager
{
    private const int ManifestVersion = 1;

    private static readonly AssetFolder[] RequiredGameFolders =
    [
        AssetFolder.Artwork,
        AssetFolder.ArtworkOriginal,
        AssetFolder.ArtworkGenerated,
        AssetFolder.ArtworkCustom,
        AssetFolder.ArtworkActive,
        AssetFolder.Saves,
        AssetFolder.Screenshots,
        AssetFolder.Videos,
        AssetFolder.Icons,
        AssetFolder.Mods,
        AssetFolder.Cache,
        AssetFolder.Temp
    ];

    private static readonly JsonSerializerOptions JsonOptions =
        CreateJsonOptions();

    /// <summary>
    /// Prevents multiple asynchronous operations from writing the
    /// same game's metadata or manifest simultaneously.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim>
        _gameLocks = new();

    private readonly AssetPaths _paths;

    /// <summary>
    /// Creates an AssetManager using NovaLauncher's default
    /// local asset directory.
    /// </summary>
    public AssetManager()
        : this(new AssetPaths())
    {
    }

    /// <summary>
    /// Creates an AssetManager using the supplied path resolver.
    ///
    /// This supports testing, portable mode, custom asset locations,
    /// and external drives.
    /// </summary>
    public AssetManager(AssetPaths paths)
    {
        _paths =
            paths ??
            throw new ArgumentNullException(nameof(paths));
    }

    /// <summary>
    /// Gets the path resolver used by this manager.
    /// </summary>
    public AssetPaths Paths => _paths;

    /// <summary>
    /// Gets the root directory containing all managed game assets.
    /// </summary>
    public string AssetsRoot => _paths.AssetsRoot;

    /// <summary>
    /// Ensures that NovaLauncher's global assets directory exists.
    /// </summary>
    public void Initialize()
    {
        Directory.CreateDirectory(AssetsRoot);
    }

    /// <summary>
    /// Creates the complete managed folder structure for a game.
    ///
    /// This synchronous method only creates directories.
    /// Use InitializeGameAsync when metadata and the Nova manifest
    /// should also be initialized.
    ///
    /// Existing directories are preserved.
    /// </summary>
    public void InitializeGame(Game game)
    {
        ValidateGame(game);

        Initialize();

        Directory.CreateDirectory(
            _paths.GetGameRoot(game));

        foreach (AssetFolder folder in RequiredGameFolders)
        {
            Directory.CreateDirectory(
                _paths.GetFolderPath(game, folder));
        }
    }

    /// <summary>
    /// Creates the complete directory structure, metadata file,
    /// and Nova manifest for a game.
    ///
    /// Existing valid files are preserved.
    /// </summary>
    public async Task<AssetMetadata> InitializeGameAsync(
        Game game,
        CancellationToken cancellationToken = default)
    {
        ValidateGame(game);

        InitializeGame(game);

        SemaphoreSlim gameLock =
            GetGameLock(game.Id);

        await gameLock.WaitAsync(cancellationToken);

        try
        {
            await EnsureManifestCoreAsync(
                game,
                cancellationToken);

            AssetMetadata metadata =
                await LoadOrCreateMetadataCoreAsync(
                    game,
                    cancellationToken);

            bool metadataChanged = false;

            if (metadata.GameId != game.Id)
            {
                throw new InvalidDataException(
                    "The asset metadata belongs to a different game.");
            }

            string normalizedName =
                NormalizeGameName(game.Name);

            if (!string.Equals(
                    metadata.GameName,
                    normalizedName,
                    StringComparison.Ordinal))
            {
                metadata.GameName =
                    normalizedName;

                metadataChanged = true;
            }

            if (metadata.SchemaVersion <
                AssetMetadata.CurrentSchemaVersion)
            {
                metadata.SchemaVersion =
                    AssetMetadata.CurrentSchemaVersion;

                metadataChanged = true;
            }

            NormalizeMetadata(metadata);

            if (metadataChanged)
            {
                metadata.Touch();

                await SaveMetadataCoreAsync(
                    game,
                    metadata,
                    cancellationToken);
            }

            return metadata;
        }
        finally
        {
            gameLock.Release();
        }
    }

    /// <summary>
    /// Creates directory structures for multiple games.
    ///
    /// A failure for one game does not prevent the remaining
    /// games from being initialized.
    ///
    /// The returned list contains games that could not be initialized.
    /// </summary>
    public IReadOnlyList<Game> InitializeGames(
        IEnumerable<Game> games)
    {
        ArgumentNullException.ThrowIfNull(games);

        List<Game> failedGames = [];

        Initialize();

        foreach (Game game in games)
        {
            try
            {
                InitializeGame(game);
            }
            catch (Exception exception) when (
                exception is IOException or
                UnauthorizedAccessException or
                InvalidOperationException or
                ArgumentException)
            {
                failedGames.Add(game);

                Debug.WriteLine(
                    $"Could not initialize assets for " +
                    $"'{game?.Name ?? "Unknown Game"}': " +
                    exception.Message);
            }
        }

        return failedGames;
    }

    /// <summary>
    /// Fully initializes multiple games, including their metadata
    /// and Nova manifests.
    ///
    /// The returned list contains games that failed initialization.
    /// </summary>
    public async Task<IReadOnlyList<Game>> InitializeGamesAsync(
        IEnumerable<Game> games,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(games);

        List<Game> failedGames = [];

        foreach (Game game in games)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await InitializeGameAsync(
                    game,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is IOException or
                UnauthorizedAccessException or
                InvalidOperationException or
                InvalidDataException or
                JsonException or
                ArgumentException)
            {
                failedGames.Add(game);

                Debug.WriteLine(
                    $"Could not fully initialize assets for " +
                    $"'{game?.Name ?? "Unknown Game"}': " +
                    exception.Message);
            }
        }

        return failedGames;
    }

    /// <summary>
    /// Gets the root asset directory for a game.
    ///
    /// This method does not create the directory.
    /// </summary>
    public string GetGameRoot(Game game)
    {
        ValidateGame(game);

        return _paths.GetGameRoot(game);
    }

    /// <summary>
    /// Gets a managed folder path for a game.
    ///
    /// This method does not create the directory.
    /// </summary>
    public string GetFolderPath(
        Game game,
        AssetFolder folder)
    {
        ValidateGame(game);

        return _paths.GetFolderPath(
            game,
            folder);
    }

    /// <summary>
    /// Gets a managed folder path and ensures that it exists.
    /// </summary>
    public string EnsureFolder(
        Game game,
        AssetFolder folder)
    {
        ValidateGame(game);

        string folderPath =
            _paths.GetFolderPath(
                game,
                folder);

        Directory.CreateDirectory(folderPath);

        return folderPath;
    }

    /// <summary>
    /// Gets the expected path for a singleton asset such as
    /// a cover, hero, logo, thumbnail, icon, or manifest.
    ///
    /// This method does not create the file.
    /// </summary>
    public string GetAssetPath(
        Game game,
        AssetFolder folder,
        AssetType assetType)
    {
        ValidateGame(game);

        return _paths.GetAssetPath(
            game,
            folder,
            assetType);
    }

    /// <summary>
    /// Gets the standard active artwork path for a game.
    /// </summary>
    public string GetActiveArtworkPath(
        Game game,
        AssetType artworkType)
    {
        ValidateGame(game);

        return _paths.GetActiveArtworkPath(
            game,
            artworkType);
    }

    /// <summary>
    /// Gets the standard generated artwork path for a game.
    /// </summary>
    public string GetGeneratedArtworkPath(
        Game game,
        AssetType artworkType)
    {
        ValidateGame(game);

        return _paths.GetGeneratedArtworkPath(
            game,
            artworkType);
    }

    /// <summary>
    /// Gets the path to the game's asset metadata file.
    /// </summary>
    public string GetMetadataPath(Game game)
    {
        ValidateGame(game);

        return _paths.GetMetadataPath(game);
    }

    /// <summary>
    /// Gets the path to the game's internal .nova manifest.
    /// </summary>
    public string GetManifestPath(Game game)
    {
        ValidateGame(game);

        return _paths.GetManifestPath(game);
    }

    /// <summary>
    /// Loads asset metadata for a game.
    ///
    /// Returns null when the metadata file does not exist.
    /// Malformed metadata throws InvalidDataException.
    /// </summary>
    public async Task<AssetMetadata?> LoadMetadataAsync(
        Game game,
        CancellationToken cancellationToken = default)
    {
        ValidateGame(game);

        SemaphoreSlim gameLock =
            GetGameLock(game.Id);

        await gameLock.WaitAsync(cancellationToken);

        try
        {
            return await LoadMetadataCoreAsync(
                game,
                cancellationToken);
        }
        finally
        {
            gameLock.Release();
        }
    }

    /// <summary>
    /// Loads existing metadata or creates a new metadata file
    /// when one does not exist.
    /// </summary>
    public async Task<AssetMetadata> LoadOrCreateMetadataAsync(
        Game game,
        CancellationToken cancellationToken = default)
    {
        ValidateGame(game);

        InitializeGame(game);

        SemaphoreSlim gameLock =
            GetGameLock(game.Id);

        await gameLock.WaitAsync(cancellationToken);

        try
        {
            return await LoadOrCreateMetadataCoreAsync(
                game,
                cancellationToken);
        }
        finally
        {
            gameLock.Release();
        }
    }

    /// <summary>
    /// Saves a game's asset metadata using an atomic replacement.
    ///
    /// The metadata is first written to a temporary file and then
    /// moved into place so interrupted writes do not leave partial JSON.
    /// </summary>
    public async Task SaveMetadataAsync(
        Game game,
        AssetMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        ValidateGame(game);
        ArgumentNullException.ThrowIfNull(metadata);

        ValidateMetadataForGame(
            game,
            metadata);

        InitializeGame(game);

        SemaphoreSlim gameLock =
            GetGameLock(game.Id);

        await gameLock.WaitAsync(cancellationToken);

        try
        {
            NormalizeMetadata(metadata);
            metadata.Touch();

            await SaveMetadataCoreAsync(
                game,
                metadata,
                cancellationToken);
        }
        finally
        {
            gameLock.Release();
        }
    }

    /// <summary>
    /// Ensures that the game has a valid Nova manifest.
    ///
    /// A missing manifest is created. A manifest belonging to another
    /// game causes InvalidDataException.
    /// </summary>
    public async Task EnsureManifestAsync(
        Game game,
        CancellationToken cancellationToken = default)
    {
        ValidateGame(game);

        InitializeGame(game);

        SemaphoreSlim gameLock =
            GetGameLock(game.Id);

        await gameLock.WaitAsync(cancellationToken);

        try
        {
            await EnsureManifestCoreAsync(
                game,
                cancellationToken);
        }
        finally
        {
            gameLock.Release();
        }
    }

    /// <summary>
    /// Checks whether the game's Nova manifest exists and belongs
    /// to the supplied game.
    /// </summary>
    public async Task<bool> HasValidManifestAsync(
        Game game,
        CancellationToken cancellationToken = default)
    {
        ValidateGame(game);

        string manifestPath =
            _paths.GetManifestPath(game);

        if (!File.Exists(manifestPath))
        {
            return false;
        }

        SemaphoreSlim gameLock =
            GetGameLock(game.Id);

        await gameLock.WaitAsync(cancellationToken);

        try
        {
            NovaAssetManifest? manifest =
                await ReadJsonFileAsync<NovaAssetManifest>(
                    manifestPath,
                    cancellationToken);

            return manifest is not null &&
                   manifest.GameId == game.Id &&
                   manifest.Version > 0;
        }
        catch (
            Exception exception) when (
                exception is IOException or
                UnauthorizedAccessException or
                JsonException or
                InvalidDataException)
        {
            Debug.WriteLine(
                $"Could not validate manifest for '{game.Name}': " +
                exception.Message);

            return false;
        }
        finally
        {
            gameLock.Release();
        }
    }

    /// <summary>
    /// Determines whether the complete game asset folder structure exists.
    ///
    /// This does not require metadata or the Nova manifest.
    /// </summary>
    public bool IsGameInitialized(Game game)
    {
        ValidateGame(game);

        string gameRoot =
            _paths.GetGameRoot(game);

        if (!Directory.Exists(gameRoot))
        {
            return false;
        }

        return RequiredGameFolders.All(
            folder =>
                Directory.Exists(
                    _paths.GetFolderPath(
                        game,
                        folder)));
    }

    /// <summary>
    /// Determines whether directories, metadata, and the manifest exist.
    ///
    /// This is a quick filesystem check and does not parse the JSON files.
    /// </summary>
    public bool IsGameFullyInitialized(Game game)
    {
        ValidateGame(game);

        return IsGameInitialized(game) &&
               File.Exists(_paths.GetMetadataPath(game)) &&
               File.Exists(_paths.GetManifestPath(game));
    }

    /// <summary>
    /// Determines whether a singleton managed asset exists.
    /// </summary>
    public bool AssetExists(
        Game game,
        AssetFolder folder,
        AssetType assetType)
    {
        string assetPath =
            GetAssetPath(
                game,
                folder,
                assetType);

        return File.Exists(assetPath);
    }

    /// <summary>
    /// Determines whether active artwork exists for a game.
    /// </summary>
    public bool ActiveArtworkExists(
        Game game,
        AssetType artworkType)
    {
        return File.Exists(
            GetActiveArtworkPath(
                game,
                artworkType));
    }

    /// <summary>
    /// Determines whether generated artwork exists for a game.
    /// </summary>
    public bool GeneratedArtworkExists(
        Game game,
        AssetType artworkType)
    {
        return File.Exists(
            GetGeneratedArtworkPath(
                game,
                artworkType));
    }

    /// <summary>
    /// Imports a file into a named managed asset location.
    ///
    /// The source file is copied and remains unchanged.
    /// </summary>
    public async Task<string> ImportFileAsync(
        Game game,
        AssetFolder destinationFolder,
        string sourcePath,
        string? destinationFileName = null,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        ValidateGame(game);

        string fullSourcePath =
            ResolveExistingSourcePath(sourcePath);

        string fileName =
            string.IsNullOrWhiteSpace(destinationFileName)
                ? Path.GetFileName(fullSourcePath)
                : destinationFileName.Trim();

        string destinationDirectory =
            EnsureFolder(
                game,
                destinationFolder);

        string destinationPath =
            _paths.GetNamedAssetPath(
                game,
                destinationFolder,
                fileName);

        if (PathsReferToSameFile(
                fullSourcePath,
                destinationPath))
        {
            return destinationPath;
        }

        if (File.Exists(destinationPath) &&
            !overwrite)
        {
            throw new IOException(
                $"An asset already exists at '{destinationPath}'.");
        }

        string temporaryPath =
            CreateTemporarySiblingPath(
                destinationDirectory,
                destinationPath,
                "import");

        try
        {
            await CopyFileAsync(
                fullSourcePath,
                temporaryPath,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            File.Move(
                temporaryPath,
                destinationPath,
                overwrite);

            return destinationPath;
        }
        catch
        {
            DeleteFileSilently(temporaryPath);
            throw;
        }
    }

    /// <summary>
    /// Copies a file into a standard singleton asset location.
    ///
    /// The supplied file must already use the correct format.
    /// This method does not convert image contents.
    /// </summary>
    public async Task<string> SetAssetAsync(
        Game game,
        AssetFolder destinationFolder,
        AssetType assetType,
        string sourcePath,
        bool overwrite = true,
        CancellationToken cancellationToken = default)
    {
        ValidateGame(game);

        string fullSourcePath =
            ResolveExistingSourcePath(sourcePath);

        string destinationPath =
            _paths.GetAssetPath(
                game,
                destinationFolder,
                assetType);

        string destinationDirectory =
            EnsureFolder(
                game,
                destinationFolder);

        if (PathsReferToSameFile(
                fullSourcePath,
                destinationPath))
        {
            return destinationPath;
        }

        if (File.Exists(destinationPath) &&
            !overwrite)
        {
            throw new IOException(
                $"An asset already exists at '{destinationPath}'.");
        }

        string temporaryPath =
            CreateTemporarySiblingPath(
                destinationDirectory,
                destinationPath,
                "set");

        try
        {
            await CopyFileAsync(
                fullSourcePath,
                temporaryPath,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            File.Move(
                temporaryPath,
                destinationPath,
                overwrite);

            return destinationPath;
        }
        catch
        {
            DeleteFileSilently(temporaryPath);
            throw;
        }
    }

    /// <summary>
    /// Copies generated artwork into the active artwork directory.
    /// </summary>
    public async Task<string> ActivateGeneratedArtworkAsync(
        Game game,
        AssetType artworkType,
        bool overwrite = true,
        CancellationToken cancellationToken = default)
    {
        ValidateGame(game);

        string generatedPath =
            _paths.GetGeneratedArtworkPath(
                game,
                artworkType);

        if (!File.Exists(generatedPath))
        {
            throw new FileNotFoundException(
                "The generated artwork could not be found.",
                generatedPath);
        }

        return await SetAssetAsync(
            game,
            AssetFolder.ArtworkActive,
            artworkType,
            generatedPath,
            overwrite,
            cancellationToken);
    }

    /// <summary>
    /// Returns all files directly inside a managed directory.
    /// </summary>
    public IReadOnlyList<string> GetFiles(
        Game game,
        AssetFolder folder,
        string searchPattern = "*")
    {
        ValidateGame(game);

        if (string.IsNullOrWhiteSpace(searchPattern))
        {
            throw new ArgumentException(
                "The search pattern cannot be empty.",
                nameof(searchPattern));
        }

        string folderPath =
            _paths.GetFolderPath(
                game,
                folder);

        if (!Directory.Exists(folderPath))
        {
            return [];
        }

        try
        {
            return Directory
                .EnumerateFiles(
                    folderPath,
                    searchPattern,
                    SearchOption.TopDirectoryOnly)
                .OrderBy(
                    path => path,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
    }

    /// <summary>
    /// Deletes one managed singleton asset.
    /// </summary>
    public bool DeleteAsset(
        Game game,
        AssetFolder folder,
        AssetType assetType)
    {
        string assetPath =
            GetAssetPath(
                game,
                folder,
                assetType);

        if (!File.Exists(assetPath))
        {
            return false;
        }

        File.Delete(assetPath);
        return true;
    }

    /// <summary>
    /// Deletes all files and subdirectories inside a managed folder,
    /// but preserves the folder itself.
    /// </summary>
    public void ClearFolder(
        Game game,
        AssetFolder folder)
    {
        ValidateGame(game);

        if (folder is
            AssetFolder.GameRoot or
            AssetFolder.Artwork)
        {
            throw new InvalidOperationException(
                $"The {folder} directory cannot be cleared directly.");
        }

        string folderPath =
            _paths.GetFolderPath(
                game,
                folder);

        if (!Directory.Exists(folderPath))
        {
            return;
        }

        foreach (
            string filePath in
            Directory.EnumerateFiles(
                folderPath,
                "*",
                SearchOption.TopDirectoryOnly))
        {
            File.Delete(filePath);
        }

        foreach (
            string directoryPath in
            Directory.EnumerateDirectories(
                folderPath,
                "*",
                SearchOption.TopDirectoryOnly))
        {
            Directory.Delete(
                directoryPath,
                recursive: true);
        }
    }

    /// <summary>
    /// Clears temporary files for one game.
    /// </summary>
    public void ClearTemporaryFiles(Game game)
    {
        ClearFolder(
            game,
            AssetFolder.Temp);
    }

    /// <summary>
    /// Clears re-creatable cache files for one game.
    /// </summary>
    public void ClearCache(Game game)
    {
        ClearFolder(
            game,
            AssetFolder.Cache);
    }

    /// <summary>
    /// Deletes a game's complete managed asset directory.
    ///
    /// This operation is destructive and should only be called
    /// after explicit user confirmation.
    /// </summary>
    public bool DeleteGameAssets(Game game)
    {
        ValidateGame(game);

        string gameRoot =
            _paths.GetGameRoot(game);

        if (!Directory.Exists(gameRoot))
        {
            return false;
        }

        Directory.Delete(
            gameRoot,
            recursive: true);

        return true;
    }

    private async Task<AssetMetadata>
        LoadOrCreateMetadataCoreAsync(
            Game game,
            CancellationToken cancellationToken)
    {
        AssetMetadata? metadata =
            await LoadMetadataCoreAsync(
                game,
                cancellationToken);

        if (metadata is not null)
        {
            ValidateMetadataForGame(
                game,
                metadata);

            NormalizeMetadata(metadata);

            return metadata;
        }

        metadata =
            AssetMetadata.Create(
                game.Id,
                game.Name);

        await SaveMetadataCoreAsync(
            game,
            metadata,
            cancellationToken);

        return metadata;
    }

    private async Task<AssetMetadata?> LoadMetadataCoreAsync(
        Game game,
        CancellationToken cancellationToken)
    {
        string metadataPath =
            _paths.GetMetadataPath(game);

        if (!File.Exists(metadataPath))
        {
            return null;
        }

        try
        {
            AssetMetadata? metadata =
                await ReadJsonFileAsync<AssetMetadata>(
                    metadataPath,
                    cancellationToken);

            if (metadata is null)
            {
                throw new InvalidDataException(
                    "The asset metadata file contains no metadata.");
            }

            ValidateMetadataForGame(
                game,
                metadata);

            NormalizeMetadata(metadata);

            return metadata;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"The asset metadata file is malformed: " +
                $"'{metadataPath}'.",
                exception);
        }
    }

    private async Task SaveMetadataCoreAsync(
        Game game,
        AssetMetadata metadata,
        CancellationToken cancellationToken)
    {
        ValidateMetadataForGame(
            game,
            metadata);

        NormalizeMetadata(metadata);

        string metadataPath =
            _paths.GetMetadataPath(game);

        await WriteJsonAtomicallyAsync(
            metadataPath,
            metadata,
            cancellationToken);
    }

    private async Task EnsureManifestCoreAsync(
        Game game,
        CancellationToken cancellationToken)
    {
        string manifestPath =
            _paths.GetManifestPath(game);

        if (File.Exists(manifestPath))
        {
            NovaAssetManifest? existingManifest;

            try
            {
                existingManifest =
                    await ReadJsonFileAsync<NovaAssetManifest>(
                        manifestPath,
                        cancellationToken);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException(
                    $"The Nova asset manifest is malformed: " +
                    $"'{manifestPath}'.",
                    exception);
            }

            if (existingManifest is null)
            {
                throw new InvalidDataException(
                    "The Nova asset manifest contains no data.");
            }

            if (existingManifest.GameId != game.Id)
            {
                throw new InvalidDataException(
                    "The asset directory belongs to a different game.");
            }

            if (existingManifest.Version <= 0)
            {
                throw new InvalidDataException(
                    "The Nova asset manifest has an invalid version.");
            }

            return;
        }

        NovaAssetManifest manifest =
            new()
            {
                Version =
                    ManifestVersion,

                GameId =
                    game.Id,

                GameName =
                    NormalizeGameName(game.Name),

                CreatedAt =
                    DateTimeOffset.UtcNow
            };

        await WriteJsonAtomicallyAsync(
            manifestPath,
            manifest,
            cancellationToken);
    }

    private static async Task<T?> ReadJsonFileAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream =
            new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

        return await JsonSerializer.DeserializeAsync<T>(
            stream,
            JsonOptions,
            cancellationToken);
    }

    private static async Task WriteJsonAtomicallyAsync<T>(
        string destinationPath,
        T value,
        CancellationToken cancellationToken)
    {
        string? directory =
            Path.GetDirectoryName(destinationPath);

        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException(
                "The JSON destination directory could not be resolved.");
        }

        Directory.CreateDirectory(directory);

        string temporaryPath =
            Path.Combine(
                directory,
                $".{Path.GetFileName(destinationPath)}." +
                $"{Guid.NewGuid():N}.tmp");

        try
        {
            await using (
                FileStream stream =
                    new(
                        temporaryPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 4096,
                        useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    value,
                    JsonOptions,
                    cancellationToken);

                await stream.FlushAsync(
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            File.Move(
                temporaryPath,
                destinationPath,
                overwrite: true);
        }
        catch
        {
            DeleteFileSilently(temporaryPath);
            throw;
        }
    }

    private static async Task CopyFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using FileStream sourceStream =
            new(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);

        await using FileStream destinationStream =
            new(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

        await sourceStream.CopyToAsync(
            destinationStream,
            cancellationToken);

        await destinationStream.FlushAsync(
            cancellationToken);
    }

    private static string ResolveExistingSourcePath(
        string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException(
                "The source path cannot be empty.",
                nameof(sourcePath));
        }

        string fullSourcePath =
            Path.GetFullPath(
                Environment.ExpandEnvironmentVariables(
                    sourcePath.Trim()));

        if (!File.Exists(fullSourcePath))
        {
            throw new FileNotFoundException(
                "The source asset file could not be found.",
                fullSourcePath);
        }

        return fullSourcePath;
    }

    private SemaphoreSlim GetGameLock(Guid gameId)
    {
        return _gameLocks.GetOrAdd(
            gameId,
            static _ => new SemaphoreSlim(1, 1));
    }

    private static void ValidateGame(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (game.Id == Guid.Empty)
        {
            throw new InvalidOperationException(
                "A game must have a valid ID before its assets can be managed.");
        }
    }

    private static void ValidateMetadataForGame(
        Game game,
        AssetMetadata metadata)
    {
        if (metadata.GameId == Guid.Empty)
        {
            throw new InvalidDataException(
                "Asset metadata does not contain a valid game ID.");
        }

        if (metadata.GameId != game.Id)
        {
            throw new InvalidDataException(
                "The asset metadata belongs to a different game.");
        }

        if (metadata.SchemaVersion <= 0)
        {
            throw new InvalidDataException(
                "The asset metadata has an invalid schema version.");
        }

        if (metadata.SchemaVersion >
            AssetMetadata.CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"The asset metadata uses schema version " +
                $"{metadata.SchemaVersion}, but this NovaLauncher build " +
                $"only supports up to version " +
                $"{AssetMetadata.CurrentSchemaVersion}.");
        }
    }

    /// <summary>
    /// Repairs nullable collection and nested-object properties that
    /// may be missing from older or manually edited JSON files.
    /// </summary>
    private static void NormalizeMetadata(
        AssetMetadata metadata)
    {
        metadata.GameName =
            NormalizeGameName(metadata.GameName);

        metadata.Artwork ??=
            new ArtworkAssetMetadata();

        metadata.Saves ??=
            new SaveAssetMetadata();

        metadata.Screenshots ??=
            new ScreenshotAssetMetadata();

        metadata.Videos ??=
            new VideoAssetMetadata();

        metadata.Mods ??=
            new ModAssetMetadata();

        metadata.Extensions ??=
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        metadata.Artwork.Sources ??=
            [];

        metadata.Artwork.Items ??=
            [];

        RemoveBlankArtworkSources(
            metadata.Artwork.Sources);

        NormalizeArtworkRecords(
            metadata.Artwork.Items);
    }

    private static void RemoveBlankArtworkSources(
        List<string> sources)
    {
        for (int index = sources.Count - 1;
             index >= 0;
             index--)
        {
            if (string.IsNullOrWhiteSpace(
                    sources[index]))
            {
                sources.RemoveAt(index);
                continue;
            }

            sources[index] =
                sources[index].Trim();
        }

        List<string> uniqueSources =
            sources
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        sources.Clear();
        sources.AddRange(uniqueSources);
    }

    private static void NormalizeArtworkRecords(
        List<ArtworkAssetRecord> records)
    {
        foreach (ArtworkAssetRecord record in records)
        {
            if (record.Id == Guid.Empty)
            {
                record.Id =
                    Guid.NewGuid();
            }

            record.Source =
                record.Source?.Trim() ??
                string.Empty;

            record.RelativePath =
                record.RelativePath?.Trim() ??
                string.Empty;

            record.SourceLocation =
                string.IsNullOrWhiteSpace(
                    record.SourceLocation)
                    ? null
                    : record.SourceLocation.Trim();

            record.HashAlgorithm =
                string.IsNullOrWhiteSpace(
                    record.HashAlgorithm)
                    ? null
                    : record.HashAlgorithm.Trim();

            record.FileHash =
                string.IsNullOrWhiteSpace(
                    record.FileHash)
                    ? null
                    : record.FileHash.Trim();
        }
    }

    private static string NormalizeGameName(
        string? gameName)
    {
        return string.IsNullOrWhiteSpace(gameName)
            ? "Unknown Game"
            : gameName.Trim();
    }

    private static string CreateTemporarySiblingPath(
        string destinationDirectory,
        string destinationPath,
        string operationName)
    {
        return Path.Combine(
            destinationDirectory,
            $".{Path.GetFileName(destinationPath)}." +
            $"{Guid.NewGuid():N}.{operationName}");
    }

    private static bool PathsReferToSameFile(
        string firstPath,
        string secondPath)
    {
        string normalizedFirstPath =
            Path.GetFullPath(firstPath)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

        string normalizedSecondPath =
            Path.GetFullPath(secondPath)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

        return string.Equals(
            normalizedFirstPath,
            normalizedSecondPath,
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
    }

    private static void DeleteFileSilently(
        string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Cleanup failure should not replace the original exception.
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions options =
            new()
            {
                PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase,

                PropertyNameCaseInsensitive =
                    true,

                WriteIndented =
                    true,

                AllowTrailingCommas =
                    true,

                ReadCommentHandling =
                    JsonCommentHandling.Skip
            };

        options.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase));

        return options;
    }

    /// <summary>
    /// Lightweight ownership marker stored in each game asset root.
    ///
    /// The .nova manifest identifies the folder even when it is
    /// renamed, copied, restored, or moved to another installation.
    /// </summary>
    private sealed class NovaAssetManifest
    {
        public int Version { get; set; }

        public Guid GameId { get; set; }

        public string GameName { get; set; } =
            string.Empty;

        public DateTimeOffset CreatedAt { get; set; }
    }
}