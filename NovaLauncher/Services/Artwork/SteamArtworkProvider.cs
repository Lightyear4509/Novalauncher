using System;
using System.Collections.Generic;
using NovaLauncher.Models;

namespace NovaLauncher.Services.Artwork;

public sealed class SteamArtworkProvider : IArtworkProvider
{
    public string ProviderName => "Steam";

    public bool CanProvideArtwork(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        return string.Equals(
                   game.Source,
                   "Steam",
                   StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(game.SteamAppId);
    }

    public IEnumerable<Uri> GetArtworkUris(
        Game game,
        ArtworkType artworkType)
    {
        if (!CanProvideArtwork(game))
        {
            yield break;
        }

        string appId = game.SteamAppId!;

        string[] fileNames = artworkType switch
        {
            ArtworkType.Cover =>
            [
                "library_600x900.jpg",
                "header.jpg",
                "capsule_616x353.jpg",
                "library_hero.jpg"
            ],

            ArtworkType.Hero =>
            [
                "library_hero.jpg",
                "capsule_616x353.jpg",
                "header.jpg"
            ],

            ArtworkType.Logo =>
            [
                "logo.png",
                "logo_2x.png"
            ],

            ArtworkType.Background =>
            [
                "library_hero.jpg",
                "page_bg_generated_v6b.jpg",
                "header.jpg"
            ],

            _ => []
        };

        foreach (string fileName in fileNames)
        {
            yield return new Uri(
                $"https://cdn.cloudflare.steamstatic.com/" +
                $"steam/apps/{appId}/{fileName}");
        }
    }
}