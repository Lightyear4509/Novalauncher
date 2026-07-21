using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
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

        // Temporary alias while the remaining event handlers
        // are gradually moved into the ViewModel.
        _games = _viewModel.Games;

        UpdateLibraryCount();

        if (_games.Count > 0)
        {
            SetStatus("Saved library loaded.");
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
                            new FilePickerFileType(
                                "Windows executable")
                            {
                                Patterns = ["*.exe"]
                            }
                        ]
                    });

            if (selectedFiles.Count == 0)
            {
                SetStatus("No file was selected.");
                return;
            }

            string? executablePath =
                selectedFiles[0].TryGetLocalPath();

            if (string.IsNullOrWhiteSpace(executablePath))
            {
                SetStatus(
                    "The selected file does not have a local path.");

                return;
            }

            bool alreadyExists = _games.Any(game =>
                game is not null &&
                string.Equals(
                    game.ExecutablePath,
                    executablePath,
                    StringComparison.OrdinalIgnoreCase));

            if (alreadyExists)
            {
                SetStatus(
                    "That game is already in your library.");

                return;
            }

            Game newGame = new()
            {
                Name =
                    Path.GetFileNameWithoutExtension(executablePath),

                ExecutablePath = executablePath
            };

            _games.Add(newGame);
            _viewModel.SelectedGame = newGame;

            SaveLibrary();
            UpdateLibraryCount();

            SetStatus(
                $"Added {newGame.Name} to the library.");
        }
        catch (Exception exception)
        {
            SetStatus(
                $"Could not add the game. {exception.Message}");
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

        _viewModel.SelectedGame = selectedGame;

        GameNameTextBox.IsEnabled = true;
        SaveNameButton.IsEnabled = true;
        ChooseCoverButton.IsEnabled = true;

        DisplayCover(selectedGame);

        SetStatus($"{selectedGame.Name} is selected.");
    }

    private async void ChooseCoverButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        Game? selectedGame = GetSelectedGame();

        if (selectedGame is null)
        {
            SetStatus("Select a game first.");
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
                SetStatus(
                    "No cover image was selected.");

                return;
            }

            string? imagePath =
                selectedFiles[0].TryGetLocalPath();

            if (string.IsNullOrWhiteSpace(imagePath))
            {
                SetStatus(
                    "The image does not have a local path.");

                return;
            }

            selectedGame.CoverImagePath = imagePath;

            SaveLibrary();
            DisplayCover(selectedGame);

            SetStatus(
                $"Cover updated for {selectedGame.Name}.");
        }
        catch (Exception exception)
        {
            SetStatus(
                $"Could not load the cover. {exception.Message}");
        }
    }

    private void DisplayCover(Game? game)
    {
        CoverImage.Source = null;

        if (game is null)
        {
            CoverImage.IsVisible = false;
            CoverPlaceholder.IsVisible = true;
            RemoveCoverButton.IsEnabled = false;
            return;
        }

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
            CoverImage.Source =
                new Bitmap(game.CoverImagePath!);

            CoverImage.IsVisible = true;
            CoverPlaceholder.IsVisible = false;
            RemoveCoverButton.IsEnabled = true;
        }
        catch
        {
            CoverImage.IsVisible = false;
            CoverPlaceholder.IsVisible = true;
            RemoveCoverButton.IsEnabled = false;

            SetStatus(
                "The selected cover image could not be displayed.");
        }
    }

    private void RemoveCoverButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        Game? selectedGame = GetSelectedGame();

        if (selectedGame is null)
        {
            SetStatus("Select a game first.");
            return;
        }

        selectedGame.CoverImagePath = null;

        SaveLibrary();
        DisplayCover(selectedGame);

        SetStatus(
            $"Removed the cover for {selectedGame.Name}.");
    }

    private Game? GetSelectedGame()
    {
        return _viewModel.SelectedGame;
    }

    private void SaveLibrary()
    {
        bool savedSuccessfully =
            _libraryService.SaveGames(_games);

        if (!savedSuccessfully)
        {
            SetStatus(
                "NovaLauncher could not save the library.");
        }
    }

    private void UpdateLibraryCount()
    {
        _viewModel.UpdateLibraryCount();
    }

    private void ClearSelectedGame()
    {
        _viewModel.SelectedGame = null;

        GameNameTextBox.IsEnabled = false;
        SaveNameButton.IsEnabled = false;
        ChooseCoverButton.IsEnabled = false;
        RemoveCoverButton.IsEnabled = false;

        CoverImage.Source = null;
        CoverImage.IsVisible = false;
        CoverPlaceholder.IsVisible = true;
    }

    private void SetStatus(string message)
    {
        _viewModel.StatusText = $"Status: {message}";
    }
}