using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
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
    private string gameName = string.Empty;

    [ObservableProperty]
    private string selectedGamePath = "Choose a game from your library.";

    [ObservableProperty]
    private string libraryCount = "0 games";

    [ObservableProperty]
    private string statusText = "Status: Ready.";

    partial void OnSelectedGameChanged(Game? value)
    {
        if (value is null)
        {
            GameName = string.Empty;
            SelectedGamePath = "Choose a game from your library.";
            return;
        }

        GameName = value.Name;
        SelectedGamePath = value.ExecutablePath;
    }

    public MainWindowViewModel()
    {
        _libraryService = new GameLibraryService();

        Games = new ObservableCollection<Game>(
            _libraryService.LoadGames());

        UpdateLibraryCount();

        if (Games.Count > 0)
        {
            SelectedGame = Games[0];
            StatusText = "Status: Saved library loaded.";
        }
    }
    public void UpdateLibraryCount()
    {
        LibraryCount =
            $"{Games.Count} {(Games.Count == 1 ? "game" : "games")}";
    }
}   