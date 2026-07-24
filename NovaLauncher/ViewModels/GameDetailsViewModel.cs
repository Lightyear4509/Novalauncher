using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovaLauncher.Models;
using System;
using System.Threading.Tasks;

namespace NovaLauncher.ViewModels;

public partial class GameDetailsViewModel : ObservableObject
{
    private readonly Action _saveName;
    private readonly Func<Task> _chooseCover;
    private readonly Action _toggleFavorite;
    private readonly Action _removeCover;
    private readonly Action _launchGame;
    private readonly Action _removeGame;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedGame))]
    [NotifyPropertyChangedFor(nameof(FavoriteButtonText))]
    private Game? selectedGame;

    [ObservableProperty]
    private string gameName = string.Empty;

    public bool HasSelectedGame => SelectedGame is not null;

    public string SelectedGamePath =>
        string.IsNullOrWhiteSpace(SelectedGame?.ExecutablePath)
            ? "No executable path available."
            : SelectedGame.ExecutablePath;

    public string FavoriteButtonText =>
        SelectedGame?.IsFavorite == true
            ? "★ Unfavorite"
            : "★ Favorite";

    public GameDetailsViewModel(
        Action saveName,
        Func<Task> chooseCover,
        Action toggleFavorite,
        Action removeCover,
        Action launchGame,
        Action removeGame)
    {
        _saveName = saveName;
        _chooseCover = chooseCover;
        _toggleFavorite = toggleFavorite;
        _removeCover = removeCover;
        _launchGame = launchGame;
        _removeGame = removeGame;
    }

    partial void OnSelectedGameChanged(Game? value)
    {
        GameName = value?.Name ?? string.Empty;

        OnPropertyChanged(nameof(SelectedGamePath));
    }

    public void RefreshSelectedGame()
    {
        GameName = SelectedGame?.Name ?? string.Empty;

        OnPropertyChanged(nameof(SelectedGame));
        OnPropertyChanged(nameof(SelectedGamePath));
        OnPropertyChanged(nameof(FavoriteButtonText));
        OnPropertyChanged(nameof(HasSelectedGame));
    }

    [RelayCommand]
    private void SaveName()
    {
        _saveName();
        RefreshSelectedGame();
    }

    [RelayCommand]
    private async Task ChooseCover()
    {
        await _chooseCover();
        RefreshSelectedGame();
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        _toggleFavorite();
        RefreshSelectedGame();
    }

    [RelayCommand]
    private void RemoveCover()
    {
        _removeCover();
        RefreshSelectedGame();
    }

    [RelayCommand]
    private void LaunchGame()
    {
        _launchGame();
    }

    [RelayCommand]
    private void RemoveGame()
    {
        _removeGame();
        RefreshSelectedGame();
    }
}