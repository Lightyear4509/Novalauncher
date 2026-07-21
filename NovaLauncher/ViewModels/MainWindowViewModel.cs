using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovaLauncher.Models;
using NovaLauncher.Services;

namespace NovaLauncher.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly GameLibraryService _libraryService;

    public ObservableCollection<Game> Games { get; }

    [ObservableProperty]
    private Game? selectedGame;

    [ObservableProperty]
    private string statusText = "Status: Ready.";

    [ObservableProperty]
    private string gameName = string.Empty;

    [ObservableProperty]
    private string selectedGamePath =
        "Choose a game from your library.";

    [ObservableProperty]
    private string libraryCount = "0 games";

    public MainWindowViewModel()
    {
        _libraryService = new GameLibraryService();

        Games = new ObservableCollection<Game>(
            _libraryService.LoadGames());

        // Remove any null entries that may exist in an older
        // or malformed games.json file.
        for (int index = Games.Count - 1; index >= 0; index--)
        {
            if (Games[index] is null)
            {
                Games.RemoveAt(index);
            }
        }

        UpdateLibraryCount();

        if (Games.Count > 0)
        {
            SelectedGame = Games[0];
            StatusText = "Status: Saved library loaded.";
        }
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

        SaveLibrary();

        StatusText =
            $"Status: Renamed game to {gameToRename.Name}.";
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
}