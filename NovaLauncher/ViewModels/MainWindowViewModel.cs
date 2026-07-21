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
    private string statusText = "Status: Ready.";

    public MainWindowViewModel()
    {
        _libraryService = new GameLibraryService();

        Games = new ObservableCollection<Game>(
            _libraryService.LoadGames());

        if (Games.Count > 0)
        {
            SelectedGame = Games[0];
            StatusText = "Status: Saved library loaded.";
        }
    }
}