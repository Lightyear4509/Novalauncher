using System;
using System.Collections.Generic;
using NovaLauncher.Models;

namespace NovaLauncher.Services.Artwork;

public interface IArtworkProvider
{
    string ProviderName { get; }

    bool CanProvideArtwork(Game game);

    IEnumerable<Uri> GetArtworkUris(
        Game game,
        ArtworkType artworkType);
}