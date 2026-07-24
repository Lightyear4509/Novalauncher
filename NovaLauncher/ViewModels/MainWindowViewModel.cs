using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovaLauncher.Models;
using NovaLauncher.Services;
using NovaLauncher.Services.Artwork;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NovaLauncher.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly GameLibraryService _libraryService;
    private readonly IFileDialogService _fileDialogService;
    private readonly SteamLibraryService _steamLibraryService = new();
    private readonly ArtworkCache _artworkCache;
    private readonly ArtworkService _artworkService;

    public ObservableCollection<Game> Games { get; }

    public ObservableCollection<Game> FilteredGames { get; }

    public string[] SortOptions { get; } =
    {
        "Name: A-Z",
        "Name: Z-A",
        "Recently Added",
        "Oldest Added",
        "Most Played",
        "Recently Played"
    };

    [ObservableProperty]
    private Game? selectedGame;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string statusText = "Status: Ready.";

    [ObservableProperty]
    private string libraryCount = "0 games";

    [ObservableProperty]
    private string selectedSortOption = "Name: A-Z";

    [ObservableProperty]
    private string selectedLibraryView = "Library";

    [ObservableProperty]
    private bool isGamePageVisible;

    [ObservableProperty]
    private string editableGameName = string.Empty;

    [ObservableProperty]
    private bool isGameRunning;

    public bool IsLibraryPageVisible => !IsGamePageVisible;

    public bool HasSelectedGame => SelectedGame is not null;

    public string FavoriteButtonText =>
        SelectedGame?.IsFavorite == true
            ? "★ Unfavorite"
            : "☆ Favorite";

    public string SelectedGamePath =>
        string.IsNullOrWhiteSpace(SelectedGame?.ExecutablePath)
            ? SelectedGame?.Source == "Steam"
                ? "Launched through Steam"
                : "No executable path available"
            : SelectedGame.ExecutablePath;

    public string SelectedGameSource =>
        string.IsNullOrWhiteSpace(SelectedGame?.Source)
            ? "Unknown"
            : SelectedGame.Source;

    public string SelectedGamePlatform =>
        string.IsNullOrWhiteSpace(SelectedGame?.Platform)
            ? "PC"
            : SelectedGame.Platform;

    public string SelectedGamePlayTime =>
        FormatPlayTime(SelectedGame?.TotalPlayTimeSeconds ?? 0);

    public string SelectedGameLastPlayed =>
        SelectedGame?.LastPlayedAt is DateTime lastPlayed
            ? lastPlayed.ToString("MMM d, yyyy 'at' h:mm tt")
            : "Never played";

    public string SelectedGameDateAdded =>
        SelectedGame?.AddedAt.ToString("MMM d, yyyy") ??
        "Unknown";

    public string SelectedGameLastSaved =>
        SelectedGame?.LastSaveActivityAt is DateTime lastSaved
            ? lastSaved.ToString("MMM d, yyyy 'at' h:mm tt")
            : string.IsNullOrWhiteSpace(SelectedGame?.SaveFolderPath)
                ? "Save folder not configured"
                : "No save activity found";

    public string SelectedGameSaveFolder =>
        string.IsNullOrWhiteSpace(SelectedGame?.SaveFolderPath)
            ? "Not configured"
            : SelectedGame.SaveFolderPath;

    public string PlayButtonText =>
        IsGameRunning
            ? "RUNNING"
            : "PLAY";

    public MainWindowViewModel(
        IFileDialogService fileDialogService)
    {
        _fileDialogService = fileDialogService;
        _libraryService = new GameLibraryService();

        _artworkCache = new ArtworkCache();

        _artworkService = new ArtworkService(
            _artworkCache,
            new IArtworkProvider[]
            {
                new SteamArtworkProvider()
            });

        Games = new ObservableCollection<Game>(
            _libraryService.LoadGames());

        for (int index = Games.Count - 1; index >= 0; index--)
        {
            if (Games[index] is null)
            {
                Games.RemoveAt(index);
            }
        }

        foreach (Game game in Games)
        {
            EnsureGameDefaults(game);
            UpdateLastSaveActivity(game);
        }

        FilteredGames = new ObservableCollection<Game>();

        RefreshFilteredGames();
        UpdateLibraryCount();

        SelectedGame = null;
        IsGamePageVisible = false;

        if (Games.Count > 0)
        {
            StatusText = "Status: Saved library loaded.";
        }
    }

    partial void OnSelectedSortOptionChanged(string value)
    {
        RefreshFilteredGames();
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshFilteredGames();
    }

    partial void OnIsGamePageVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsLibraryPageVisible));
    }

    partial void OnIsGameRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(PlayButtonText));
    }

    partial void OnSelectedGameChanged(Game? value)
    {
        EditableGameName = value?.Name ?? string.Empty;

        if (value is not null)
        {
            EnsureGameDefaults(value);
            UpdateLastSaveActivity(value);

            IsGamePageVisible = true;
        }

        NotifySelectedGameDetails();
    }

    private static void EnsureGameDefaults(Game game)
    {
        if (game.AddedAt == default)
        {
            game.AddedAt = DateTime.Now;
        }

        if (string.IsNullOrWhiteSpace(game.Platform))
        {
            game.Platform = "PC";
        }

        if (string.IsNullOrWhiteSpace(game.Source))
        {
            game.Source = "Manual";
        }
    }

    private void NotifySelectedGameDetails()
    {
        OnPropertyChanged(nameof(HasSelectedGame));
        OnPropertyChanged(nameof(FavoriteButtonText));
        OnPropertyChanged(nameof(SelectedGamePath));
        OnPropertyChanged(nameof(SelectedGameSource));
        OnPropertyChanged(nameof(SelectedGamePlatform));
        OnPropertyChanged(nameof(SelectedGamePlayTime));
        OnPropertyChanged(nameof(SelectedGameLastPlayed));
        OnPropertyChanged(nameof(SelectedGameDateAdded));
        OnPropertyChanged(nameof(SelectedGameLastSaved));
        OnPropertyChanged(nameof(SelectedGameSaveFolder));
        OnPropertyChanged(nameof(PlayButtonText));
    }

    public void UpdateLibraryCount()
    {
        LibraryCount =
            $"{Games.Count} {(Games.Count == 1 ? "game" : "games")}";
    }

    [RelayCommand]
    private void BackToLibrary()
    {
        IsGamePageVisible = false;
        SelectedGame = null;

        StatusText = "Status: Returned to library.";
    }

    [RelayCommand]
    private void SaveName()
    {
        Game? gameToRename = SelectedGame;

        if (gameToRename is null)
        {
            StatusText = "Status: Select a game first.";
            return;
        }

        string newName =
            EditableGameName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(newName))
        {
            StatusText =
                "Status: The game name cannot be empty.";

            EditableGameName =
                gameToRename.Name ?? string.Empty;

            return;
        }

        gameToRename.Name = newName;
        EditableGameName = newName;

        SaveLibrary();
        RefreshFilteredGames();
        NotifySelectedGameDetails();

        StatusText =
            $"Status: Renamed game to {newName}.";
    }

    [RelayCommand]
    private void ShowLibrary()
    {
        SelectedLibraryView = "Library";
        IsGamePageVisible = false;
        SelectedGame = null;

        RefreshFilteredGames();
    }

    [RelayCommand]
    private void ShowFavorites()
    {
        SelectedLibraryView = "Favorites";
        IsGamePageVisible = false;
        SelectedGame = null;

        RefreshFilteredGames();
    }

    [RelayCommand]
    private void ShowRecentlyAdded()
    {
        SelectedLibraryView = "Recently Added";
        IsGamePageVisible = false;
        SelectedGame = null;

        RefreshFilteredGames();
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        Game? selectedGame = SelectedGame;

        if (selectedGame is null)
        {
            StatusText = "Status: Select a game first.";
            return;
        }

        selectedGame.IsFavorite =
            !selectedGame.IsFavorite;

        SaveLibrary();
        RefreshFilteredGames();
        NotifySelectedGameDetails();

        StatusText = selectedGame.IsFavorite
            ? $"Status: {selectedGame.Name} was added to favorites."
            : $"Status: {selectedGame.Name} was removed from favorites.";
    }

    [RelayCommand]
    private void RemoveCover()
    {
        Game? selectedGame = SelectedGame;

        if (selectedGame is null)
        {
            StatusText = "Status: Select a game first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(
                selectedGame.CoverImagePath))
        {
            StatusText =
                "Status: This game does not have a cover.";

            return;
        }

        selectedGame.CoverImagePath = null;

        SaveLibrary();
        NotifySelectedGameDetails();

        StatusText =
            $"Status: Removed the cover for {selectedGame.Name}.";
    }

    [RelayCommand]
    private async Task ChooseCover()
    {
        Game? selectedGame = SelectedGame;

        if (selectedGame is null)
        {
            StatusText = "Status: Select a game first.";
            return;
        }

        try
        {
            string? imagePath =
                await _fileDialogService.PickCoverImageAsync();

            if (string.IsNullOrWhiteSpace(imagePath))
            {
                StatusText =
                    "Status: No cover image was selected.";

                return;
            }

            selectedGame.CoverImagePath = imagePath;

            SaveLibrary();
            NotifySelectedGameDetails();

            StatusText =
                $"Status: Cover updated for {selectedGame.Name}.";
        }
        catch (Exception exception)
        {
            StatusText =
                $"Status: Could not load the cover. {exception.Message}";
        }
    }

    [RelayCommand]
    private void RemoveGame()
    {
        Game? gameToRemove = SelectedGame;

        if (gameToRemove is null)
        {
            StatusText = "Status: Select a game first.";
            return;
        }

        string removedGameName = gameToRemove.Name;

        Games.Remove(gameToRemove);

        SelectedGame = null;
        IsGamePageVisible = false;

        SaveLibrary();
        RefreshFilteredGames();
        UpdateLibraryCount();

        StatusText =
            $"Status: Removed {removedGameName} from the library.";
    }

    [RelayCommand]
    private async Task LaunchGame()
    {
        Game? gameToLaunch = SelectedGame;

        if (gameToLaunch is null)
        {
            StatusText = "Status: Select a game first.";
            return;
        }

        if (IsGameRunning)
        {
            StatusText =
                "Status: A game session is already being tracked.";

            return;
        }

        if (string.Equals(
                gameToLaunch.Source,
                "Steam",
                StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(
                gameToLaunch.SteamAppId))
        {
            LaunchSteamGame(gameToLaunch);
            return;
        }

        await LaunchLocalGameAsync(gameToLaunch);
    }

    private void LaunchSteamGame(Game gameToLaunch)
    {
        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName =
                        $"steam://rungameid/{gameToLaunch.SteamAppId}",

                    UseShellExecute = true
                });

            gameToLaunch.LastPlayedAt = DateTime.Now;

            UpdateLastSaveActivity(gameToLaunch);
            SaveLibrary();
            NotifySelectedGameDetails();

            StatusText =
                $"Status: Launching {gameToLaunch.Name} through Steam. " +
                "NovaLauncher cannot track the exact Steam session length yet.";
        }
        catch (Exception exception)
        {
            StatusText =
                $"Status: Could not launch Steam game. {exception.Message}";
        }
    }

    private async Task LaunchLocalGameAsync(
        Game gameToLaunch)
    {
        if (string.IsNullOrWhiteSpace(
                gameToLaunch.ExecutablePath))
        {
            StatusText =
                "Status: This game does not have a valid executable path.";

            return;
        }

        if (!File.Exists(gameToLaunch.ExecutablePath))
        {
            StatusText =
                "Status: The executable could not be found. " +
                "It may have been moved or deleted.";

            return;
        }

        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = gameToLaunch.ExecutablePath,

                WorkingDirectory =
                    Path.GetDirectoryName(
                        gameToLaunch.ExecutablePath) ??
                    string.Empty,

                UseShellExecute = true
            };

            DateTime sessionStartedAt = DateTime.Now;

            Process? process = Process.Start(startInfo);

            if (process is null)
            {
                StatusText =
                    "Status: Windows did not start the game process.";

                return;
            }

            gameToLaunch.LastPlayedAt =
                sessionStartedAt;

            IsGameRunning = true;

            SaveLibrary();
            NotifySelectedGameDetails();

            StatusText =
                $"Status: {gameToLaunch.Name} is running.";

            await process.WaitForExitAsync();

            DateTime sessionEndedAt = DateTime.Now;

            long sessionSeconds = Math.Max(
                0,
                (long)(sessionEndedAt -
                       sessionStartedAt).TotalSeconds);

            gameToLaunch.TotalPlayTimeSeconds +=
                sessionSeconds;

            UpdateLastSaveActivity(gameToLaunch);

            IsGameRunning = false;

            SaveLibrary();
            RefreshFilteredGames();
            NotifySelectedGameDetails();

            StatusText =
                $"Status: {gameToLaunch.Name} closed. " +
                $"Session time: {FormatPlayTime(sessionSeconds)}.";
        }
        catch (Exception exception)
        {
            IsGameRunning = false;

            StatusText =
                $"Status: Launch failed. {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task AddGame()
    {
        try
        {
            string? executablePath =
                await _fileDialogService.PickExecutableAsync();

            if (string.IsNullOrWhiteSpace(executablePath))
            {
                StatusText =
                    "Status: No file was selected.";

                return;
            }

            bool alreadyExists = Games.Any(game =>
                game is not null &&
                string.Equals(
                    game.ExecutablePath,
                    executablePath,
                    StringComparison.OrdinalIgnoreCase));

            if (alreadyExists)
            {
                StatusText =
                    "Status: That game is already in your library.";

                return;
            }

            Game newGame = new()
            {
                Name =
                    Path.GetFileNameWithoutExtension(
                        executablePath),

                ExecutablePath = executablePath,
                AddedAt = DateTime.Now,
                Source = "Manual",
                Platform = "PC"
            };

            Games.Add(newGame);

            SaveLibrary();
            RefreshFilteredGames();
            UpdateLibraryCount();

            StatusText =
                $"Status: Added {newGame.Name} to the library.";
        }
        catch (Exception exception)
        {
            StatusText =
                $"Status: Could not add the game. {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportSteamGames()
    {
        try
        {
            StatusText =
                "Status: Searching for installed Steam games...";

            var discoveredGames =
                _steamLibraryService.FindInstalledGames();

            if (discoveredGames.Count == 0)
            {
                StatusText =
                    "Status: No installed Steam games were found.";

                return;
            }

            int importedCount = 0;
            int skippedCount = 0;
            int artworkCount = 0;

            foreach (var steamGame in discoveredGames)
            {
                Game? existingGame =
                    Games.FirstOrDefault(game =>
                        string.Equals(
                            game.Source,
                            "Steam",
                            StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(
                            game.SteamAppId,
                            steamGame.AppId,
                            StringComparison.OrdinalIgnoreCase));

                if (existingGame is not null)
                {
                    EnsureGameDefaults(existingGame);

                    if (string.IsNullOrWhiteSpace(
                            existingGame.CoverImagePath) ||
                        !File.Exists(
                            existingGame.CoverImagePath))
                    {
                        StatusText =
                            $"Status: Downloading artwork for {existingGame.Name}...";

                        string? existingCoverPath =
                            await _artworkService
                                .GetOrDownloadArtworkAsync(
                                    existingGame,
                                    ArtworkType.Cover);

                        if (!string.IsNullOrWhiteSpace(
                                existingCoverPath))
                        {
                            existingGame.CoverImagePath =
                                existingCoverPath;

                            artworkCount++;
                        }
                    }

                    skippedCount++;
                    continue;
                }

                Game newGame = new()
                {
                    Name = steamGame.Name,
                    Source = "Steam",
                    Platform = "Steam",
                    SteamAppId = steamGame.AppId,
                    InstallDirectory =
                        steamGame.InstallDirectory,
                    AddedAt = DateTime.Now
                };

                Games.Add(newGame);

                StatusText =
                    $"Status: Downloading artwork for {newGame.Name}...";

                string? coverPath =
                    await _artworkService
                        .GetOrDownloadArtworkAsync(
                            newGame,
                            ArtworkType.Cover);

                if (!string.IsNullOrWhiteSpace(
                        coverPath))
                {
                    newGame.CoverImagePath = coverPath;
                    artworkCount++;
                }

                importedCount++;
            }

            SaveLibrary();
            RefreshFilteredGames();
            UpdateLibraryCount();

            StatusText =
                $"Status: Imported {importedCount} Steam game(s), " +
                $"downloaded {artworkCount} cover(s), and skipped " +
                $"{skippedCount} existing game(s).";
        }
        catch (Exception exception)
        {
            StatusText =
                $"Status: Steam import failed. {exception.Message}";
        }
    }

    private void UpdateLastSaveActivity(Game game)
    {
        if (string.IsNullOrWhiteSpace(
                game.SaveFolderPath))
        {
            return;
        }

        if (!Directory.Exists(game.SaveFolderPath))
        {
            return;
        }

        try
        {
            DateTime? newestWriteTime = Directory
                .EnumerateFiles(
                    game.SaveFolderPath,
                    "*",
                    SearchOption.AllDirectories)
                .Select(filePath =>
                {
                    try
                    {
                        return (DateTime?)File
                            .GetLastWriteTime(filePath);
                    }
                    catch
                    {
                        return null;
                    }
                })
                .Where(date => date.HasValue)
                .OrderByDescending(date => date)
                .FirstOrDefault();

            if (newestWriteTime.HasValue)
            {
                game.LastSaveActivityAt =
                    newestWriteTime.Value;
            }
        }
        catch
        {
            // Some save folders may contain protected or inaccessible files.
            // The launcher should continue working even when scanning fails.
        }
    }

    private static string FormatPlayTime(
        long totalSeconds)
    {
        if (totalSeconds <= 0)
        {
            return "Not played yet";
        }

        TimeSpan playTime =
            TimeSpan.FromSeconds(totalSeconds);

        if (playTime.TotalHours >= 1)
        {
            int hours = (int)playTime.TotalHours;

            return
                $"{hours}h {playTime.Minutes}m";
        }

        if (playTime.TotalMinutes >= 1)
        {
            return $"{(int)playTime.TotalMinutes}m";
        }

        return $"{Math.Max(1, playTime.Seconds)}s";
    }

    private void SaveLibrary()
    {
        bool savedSuccessfully =
            _libraryService.SaveGames(Games);

        if (!savedSuccessfully)
        {
            StatusText =
                "Status: NovaLauncher could not save the library.";
        }
    }

    private void RefreshFilteredGames()
    {
        FilteredGames.Clear();

        string searchQuery =
            SearchText.Trim();

        IEnumerable<Game> matchingGames =
            Games;

        matchingGames =
            SelectedLibraryView switch
            {
                "Favorites" =>
                    matchingGames.Where(
                        game => game.IsFavorite),

                "Recently Added" =>
                    matchingGames
                        .OrderByDescending(
                            game => game.AddedAt)
                        .Take(10),

                _ => matchingGames
            };

        if (!string.IsNullOrWhiteSpace(
                searchQuery))
        {
            matchingGames =
                matchingGames.Where(game =>
                    game.Name.Contains(
                        searchQuery,
                        StringComparison.OrdinalIgnoreCase));
        }

        matchingGames =
            SelectedSortOption switch
            {
                "Name: Z-A" =>
                    matchingGames.OrderByDescending(
                        game => game.Name),

                "Recently Added" =>
                    matchingGames.OrderByDescending(
                        game => game.AddedAt),

                "Oldest Added" =>
                    matchingGames.OrderBy(
                        game => game.AddedAt),

                "Most Played" =>
                    matchingGames.OrderByDescending(
                        game => game.TotalPlayTimeSeconds),

                "Recently Played" =>
                    matchingGames.OrderByDescending(
                        game => game.LastPlayedAt ??
                                DateTime.MinValue),

                _ =>
                    matchingGames.OrderBy(
                        game => game.Name)
            };

        foreach (Game game in matchingGames)
        {
            FilteredGames.Add(game);
        }
    }
}