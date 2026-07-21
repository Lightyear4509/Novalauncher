using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovaLauncher.Models;
using NovaLauncher.Services;
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

    public ObservableCollection<Game> Games { get; }
    public ObservableCollection<Game> FilteredGames { get; }

    [ObservableProperty]
    private Game? selectedGame;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string statusText = "Status: Ready.";

    [ObservableProperty]
    private string gameName = string.Empty;

    [ObservableProperty]
    private string selectedGamePath =
        "Choose a game from your library.";

    [ObservableProperty]
    private string libraryCount = "0 games";

    public MainWindowViewModel(
    IFileDialogService fileDialogService)
    {
        _fileDialogService = fileDialogService;
        _libraryService = new GameLibraryService();

        Games = new ObservableCollection<Game>(
            _libraryService.LoadGames());

        for (int index = Games.Count - 1; index >= 0; index--)
        {
            if (Games[index] is null)
            {
                Games.RemoveAt(index);
            }
        }

        FilteredGames = new ObservableCollection<Game>(Games);

        UpdateLibraryCount();

        if (Games.Count > 0)
        {
            SelectedGame = Games[0];
            StatusText = "Status: Saved library loaded.";
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshFilteredGames();
    }

    partial void OnSelectedGameChanged(Game? value)
    {
        if (value is null)
        {
            GameName = string.Empty;
            SelectedGamePath =
                "Choose a game from your library.";

            return;
        }

        GameName = value.Name;
        SelectedGamePath = value.ExecutablePath;
    }

    public void UpdateLibraryCount()
    {
        LibraryCount =
            $"{Games.Count} {(Games.Count == 1 ? "game" : "games")}";
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

        string newName = GameName.Trim();

        if (string.IsNullOrWhiteSpace(newName))
        {
            StatusText =
                "Status: The game name cannot be empty.";

            GameName = gameToRename.Name;
            return;
        }

        gameToRename.Name = newName;
        RefreshFilteredGames();

        SaveLibrary();

        StatusText =
            $"Status: Renamed game to {gameToRename.Name}.";
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

        if (string.IsNullOrWhiteSpace(selectedGame.CoverImagePath))
        {
            StatusText = "Status: This game does not have a cover.";
            return;
        }

        selectedGame.CoverImagePath = null;

        SaveLibrary();

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
        RefreshFilteredGames();

        if (Games.Count > 0)
        {
            SelectedGame = Games[0];
        }
        else
        {
            SelectedGame = null;
        }

        SaveLibrary();
        UpdateLibraryCount();

        StatusText =
            $"Status: Removed {removedGameName} from the library.";
    }

    [RelayCommand]
    private void LaunchGame()
    {
        Game? gameToLaunch = SelectedGame;

        if (gameToLaunch is null)
        {
            StatusText = "Status: Select a game first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(gameToLaunch.ExecutablePath))
        {
            StatusText =
                "Status: This game does not have a valid executable path.";

            return;
        }

        if (!File.Exists(gameToLaunch.ExecutablePath))
        {
            StatusText =
                "Status: The executable could not be found. It may have been moved or deleted.";

            return;
        }

        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = gameToLaunch.ExecutablePath,
                WorkingDirectory =
                    Path.GetDirectoryName(gameToLaunch.ExecutablePath),

                UseShellExecute = true
            };

            Process.Start(startInfo);

            StatusText =
                $"Status: Launched {gameToLaunch.Name}.";
        }
        catch (Exception exception)
        {
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
                StatusText = "Status: No file was selected.";
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
                    Path.GetFileNameWithoutExtension(executablePath),

                ExecutablePath = executablePath
            };

            Games.Add(newGame);
            RefreshFilteredGames();

            SelectedGame = newGame;

            SaveLibrary();
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

        string searchQuery = SearchText.Trim();

        IEnumerable<Game> matchingGames = Games;

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            matchingGames = Games.Where(game =>
                game.Name.Contains(
                    searchQuery,
                    StringComparison.OrdinalIgnoreCase));
        }

        foreach (Game game in matchingGames)
        {
            FilteredGames.Add(game);
        }
    }
}