using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NovaLauncher.Models;
using NovaLauncher.Services;

namespace NovaLauncher.Views;

public partial class MainWindow : Window
{
    private readonly GameLibraryService _libraryService;
    private readonly ObservableCollection<Game> _games;

    public MainWindow()
    {
        InitializeComponent();

        _libraryService = new GameLibraryService();

        List<Game> savedGames = _libraryService.LoadGames();

        _games = new ObservableCollection<Game>(savedGames);

        GameList.ItemsSource = _games;

        UpdateLibraryCount();

        if (_games.Count > 0)
        {
            GameList.SelectedIndex = 0;
            StatusText.Text = "Status: Saved library loaded.";
        }
    }

    private async void AddGameButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        try
        {
            IReadOnlyList<IStorageFile> selectedFiles =
                await StorageProvider.OpenFilePickerAsync(
                    new FilePickerOpenOptions
                    {
                        Title = "Choose a game executable",
                        AllowMultiple = false,
                        FileTypeFilter =
                        [
                            new FilePickerFileType("Windows executable")
                            {
                                Patterns = ["*.exe"]
                            }
                        ]
                    });

            if (selectedFiles.Count == 0)
            {
                StatusText.Text = "Status: No file was selected.";
                return;
            }

            string? executablePath =
                selectedFiles[0].TryGetLocalPath();

            if (string.IsNullOrWhiteSpace(executablePath))
            {
                StatusText.Text =
                    "Status: The selected file does not have a local path.";

                return;
            }

            bool alreadyExists = _games.Any(game =>
                string.Equals(
                    game.ExecutablePath,
                    executablePath,
                    StringComparison.OrdinalIgnoreCase));

            if (alreadyExists)
            {
                StatusText.Text =
                    "Status: That game is already in your library.";

                return;
            }

            Game newGame = new()
            {
                Name = Path.GetFileNameWithoutExtension(executablePath),
                ExecutablePath = executablePath
            };

            _games.Add(newGame);

            GameList.SelectedItem = newGame;

            SaveLibrary();
            UpdateLibraryCount();

            StatusText.Text =
                $"Status: Added {newGame.Name} to the library.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Status: Could not add the game. {exception.Message}";
        }
    }

    private void GameList_SelectionChanged(
        object? sender,
        SelectionChangedEventArgs e)
    {
        Game? selectedGame = GetSelectedGame();

        if (selectedGame is null)
        {
            ClearSelectedGame();
            return;
        }

        SelectedGameTitleText.Text = selectedGame.Name;
        SelectedGamePathText.Text = selectedGame.ExecutablePath;

        LaunchButton.IsEnabled = true;
        RemoveButton.IsEnabled = true;

        StatusText.Text =
            $"Status: {selectedGame.Name} is selected.";
    }

    private void LaunchButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        Game? selectedGame = GetSelectedGame();

        if (selectedGame is null)
        {
            StatusText.Text =
                "Status: Select a game first.";

            return;
        }

        if (!File.Exists(selectedGame.ExecutablePath))
        {
            StatusText.Text =
                "Status: The executable could not be found. It may have been moved or deleted.";

            return;
        }

        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = selectedGame.ExecutablePath,
                WorkingDirectory =
                    Path.GetDirectoryName(selectedGame.ExecutablePath),
                UseShellExecute = true
            };

            Process.Start(startInfo);

            StatusText.Text =
                $"Status: Launched {selectedGame.Name}.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Status: Launch failed. {exception.Message}";
        }
    }

    private void RemoveButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        Game? selectedGame = GetSelectedGame();

        if (selectedGame is null)
        {
            return;
        }

        string removedGameName = selectedGame.Name;

        _games.Remove(selectedGame);

        SaveLibrary();
        UpdateLibraryCount();

        if (_games.Count > 0)
        {
            GameList.SelectedIndex = 0;
        }
        else
        {
            ClearSelectedGame();
        }

        StatusText.Text =
            $"Status: Removed {removedGameName} from the library.";
    }

    private Game? GetSelectedGame()
    {
        return GameList.SelectedItem as Game;
    }

    private void SaveLibrary()
    {
        bool savedSuccessfully =
            _libraryService.SaveGames(_games);

        if (!savedSuccessfully)
        {
            StatusText.Text =
                "Status: NovaLauncher could not save the library.";
        }
    }

    private void UpdateLibraryCount()
    {
        string gameWord =
            _games.Count == 1 ? "game" : "games";

        LibraryCountText.Text =
            $"{_games.Count} {gameWord}";
    }

    private void ClearSelectedGame()
    {
        SelectedGameTitleText.Text = "Select a game";

        SelectedGamePathText.Text =
            "Choose a game from your library.";

        LaunchButton.IsEnabled = false;
        RemoveButton.IsEnabled = false;
    }
}