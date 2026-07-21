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

        IFileDialogService fileDialogService =
            new FileDialogService(this);

        _viewModel =
            new MainWindowViewModel(fileDialogService);

        DataContext = _viewModel;

        _libraryService = new GameLibraryService();
        _games = _viewModel.Games;

        UpdateLibraryCount();

        if (_games.Count > 0)
        {
            SetStatus("Saved library loaded.");
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