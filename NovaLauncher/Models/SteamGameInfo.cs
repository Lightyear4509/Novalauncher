namespace NovaLauncher.Models;

public sealed class SteamGameInfo
{
    public string AppId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string InstallDirectory { get; init; } = string.Empty;
}