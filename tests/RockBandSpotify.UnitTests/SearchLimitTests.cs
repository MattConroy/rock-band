using RockBandSpotify.Services;

namespace RockBandSpotify.UnitTests;

/// <summary>
/// Spotify cut search's maximum limit from 50 to 10 in February 2026. Asking
/// for more is a 400, and searching is the one call this app makes that a
/// setting can turn on later — so the ceiling is pinned rather than
/// rediscovered the hard way.
/// </summary>
public class SearchLimitTests
{
    [Fact]
    public void The_ceiling_matches_what_Spotify_now_allows()
        => Assert.Equal(10, SpotifyApiService.MaxSearchLimit);

    [Fact]
    public void The_default_the_matcher_uses_is_within_it()
    {
        // MatchingService calls SearchTracksAsync without a limit, so its
        // default has to be legal on its own.
        var method = typeof(ITrackLookup).GetMethod(nameof(ITrackLookup.SearchTracksAsync))!;
        var limit = (int)method.GetParameters().Single(p => p.Name == "limit").DefaultValue!;

        Assert.InRange(limit, 1, SpotifyApiService.MaxSearchLimit);
    }
}
