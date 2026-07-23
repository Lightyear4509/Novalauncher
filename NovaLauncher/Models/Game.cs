using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NovaLauncher.Models;

public partial class Game : ObservableObject
{
    [ObservableProperty]
    private Guid id = Guid.NewGuid();

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string executablePath = string.Empty;

    [ObservableProperty]
    private DateTime addedAt = DateTime.Now;

    [ObservableProperty]
    private string? coverImagePath;

    [ObservableProperty]
    private bool isFavorite;

    public string Source { get; set; } = "Manual";

    public string? SteamAppId { get; set; }

    public string? InstallDirectory { get; set; }
}