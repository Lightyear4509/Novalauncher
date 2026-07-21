using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Media.Imaging;
using NovaLauncher.Models;
using NovaLauncher.Services;
using NovaLauncher.ViewModels;

namespace NovaLauncher.Views;

public partial class MainWindow : Window
{
    private readonly GameLibraryService _libraryService;
    private readonly ObservableCollection<Game> _games;
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;

        _libraryService = new GameLibraryService();

        // Temporarily keep this alias so the existing event handlers
        // continue working during the refactor.
        _games = _viewModel.Games;

        UpdateLibraryCount();

        if (_games.Count > 0)
        {
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

            SetStatus($"Added {newGame.Name} to the library.");
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

        GameNameTextBox.Text = selectedGame.Name;
        SelectedGamePathText.Text = selectedGame.ExecutablePath;

        GameNameTextBox.IsEnabled = true;
        SaveNameButton.IsEnabled = true;
        ChooseCoverButton.IsEnabled = true;
        LaunchButton.IsEnabled = true;
        RemoveButton.IsEnabled = true;

        DisplayCover(selectedGame);

        StatusText.Text =
            $"Status: {selectedGame.Name} is selected.";
    }

    private void SaveNameButton_Click(
    object? sender,
    RoutedEventArgs e)
    {
        Game? selectedGame = GetSelectedGame();

        if (selectedGame is null)
        {
            SetStatus("Select a game first.");
            return;
        }

        string newName = GameNameTextBox.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(newName))
        {
            StatusText.Text =
                "Status: The game name cannot be empty.";

            GameNameTextBox.Text = selectedGame.Name;
            return;
        }

        selectedGame.Name = newName;

        SaveLibrary();

        // Refresh the ListBox because Game does not yet notify the UI
        GameList.ItemsSource = null;
        GameList.ItemsSource = _games;
        GameList.SelectedItem = selectedGame;

        StatusText.Text =
            $"Status: Renamed game to {selectedGame.Name}.";
    }

    private async void ChooseCoverButton_Click(
    object? sender,
    RoutedEventArgs e)
    {
        Game? selectedGame = GetSelectedGame();

        if (selectedGame is null)
        {
            StatusText.Text = "Status: Select a game first.";
            return;
        }

        try
        {
            IReadOnlyList<IStorageFile> selectedFiles =
                await StorageProvider.OpenFilePickerAsync(
                    new FilePickerOpenOptions
                    {
                        Title = "Choose cover image",
                        AllowMultiple = false,
                        FileTypeFilter =
                        [
                            new FilePickerFileType("Image files")
                        {
                            Patterns =
                            [
                                "*.png",
                                "*.jpg",
                                "*.jpeg",
                                "*.webp",
                                "*.bmp"
                            ]
                        }
                        ]
                    });

            if (selectedFiles.Count == 0)
            {
                StatusText.Text =
                    "Status: No cover image was selected.";

                return;
            }

            string? imagePath =
                selectedFiles[0].TryGetLocalPath();

            if (string.IsNullOrWhiteSpace(imagePath))
            {
                StatusText.Text =
                    "Status: The image does not have a local path.";

                return;
            }

            selectedGame.CoverImagePath = imagePath;

            SaveLibrary();
            DisplayCover(selectedGame);

            StatusText.Text =
                $"Status: Cover updated for {selectedGame.Name}.";
        }
        catch (Exception exception)
        {
            StatusText.Text =
                $"Status: Could not load the cover. {exception.Message}";
        }
    }
    private void DisplayCover(Game game)
    {
        CoverImage.Source = null;

        bool validCover =
            !string.IsNullOrWhiteSpace(game.CoverImagePath) &&
            File.Exists(game.CoverImagePath);

        if (!validCover)
        {
            CoverImage.IsVisible = false;
            CoverPlaceholder.IsVisible = true;
            RemoveCoverButton.IsEnabled = false;
            return;
        }

        try
        {
            CoverImage.Source = new Bitmap(game.CoverImagePath!);
            CoverImage.IsVisible = true;
            CoverPlaceholder.IsVisible = false;
            RemoveCoverButton.IsEnabled = true;
        }
        catch
        {
            CoverImage.IsVisible = false;
            CoverPlaceholder.IsVisible = true;
            RemoveCoverButton.IsEnabled = false;
        }
    }

    private void RemoveCoverButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        Game? selectedGame = GetSelectedGame();

        if (selectedGame is null)
        {
            return;
        }

        selectedGame.CoverImagePath = null;

        SaveLibrary();
        DisplayCover(selectedGame);

        StatusText.Text =
            $"Status: Removed the cover for {selectedGame.Name}.";
    }

    private void LaunchButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        Game? selectedGame = GetSelectedGame();

        if (selectedGame is null)
        {
            SetStatus("Select a game first.");

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

        SetStatus($"Removed {removedGameName} from the library.");
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
        GameNameTextBox.Text = string.Empty;
        GameNameTextBox.IsEnabled = false;

        SelectedGamePathText.Text =
            "Choose a game from your library.";

        CoverImage.Source = null;
        CoverImage.IsVisible = false;
        CoverPlaceholder.IsVisible = true;

        SaveNameButton.IsEnabled = false;
        ChooseCoverButton.IsEnabled = false;
        RemoveCoverButton.IsEnabled = false;
        LaunchButton.IsEnabled = false;
        RemoveButton.IsEnabled = false;
    }
    private void SetStatus(string message)
    {
        _viewModel.StatusText = $"Status: {message}";
    }
}