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
    private string? heroImagePath;

    [ObservableProperty]
    private bool isFavorite;

    [ObservableProperty]
    private DateTime? lastPlayedAt;

    [ObservableProperty]
    private long totalPlayTimeSeconds;

    [ObservableProperty]
    private string? saveFolderPath;

    [ObservableProperty]
    private DateTime? lastSaveActivityAt;

    [ObservableProperty]
    private string platform = "PC";

    public string Source { get; set; } = "Manual";

    public string? SteamAppId { get; set; }

    public string? InstallDirectory { get; set; }
}