using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NovaLauncher.Models;

namespace NovaLauncher.Services.Artwork;

public sealed class ArtworkService
{
    private readonly ArtworkCache _artworkCache;
    private readonly IReadOnlyList<IArtworkProvider> _providers;

    public ArtworkService(
        ArtworkCache artworkCache,
        IEnumerable<IArtworkProvider> providers)
    {
        _artworkCache =
            artworkCache ??
            throw new ArgumentNullException(
                nameof(artworkCache));

        _providers =
            providers?.ToList() ??
            throw new ArgumentNullException(
                nameof(providers));
    }

    public async Task<string?> GetOrDownloadArtworkAsync(
    Game game,
    ArtworkType artworkType,
    CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);

        IArtworkProvider? provider =
            _providers.FirstOrDefault(
                candidate => candidate.CanProvideArtwork(game));

        if (provider is null)
        {
            return null;
        }

        string? gameIdentifier = GetGameIdentifier(game);

        if (string.IsNullOrWhiteSpace(gameIdentifier))
        {
            return null;
        }

        IEnumerable<Uri> artworkUris =
            provider.GetArtworkUris(
                game,
                artworkType);

        foreach (Uri artworkUri in artworkUris)
        {
            string? artworkPath =
                await _artworkCache.GetOrDownloadAsync(
                    artworkUri,
                    provider.ProviderName,
                    gameIdentifier,
                    artworkType.ToString(),
                    cancellationToken);

            if (!string.IsNullOrWhiteSpace(artworkPath))
            {
                return artworkPath;
            }
        }

        return null;
    }

    private static string? GetGameIdentifier(Game game)
    {
        if (!string.IsNullOrWhiteSpace(game.SteamAppId))
        {
            return game.SteamAppId;
        }

        if (game.Id != Guid.Empty)
        {
            return game.Id.ToString();
        }

        return null;
    }
}