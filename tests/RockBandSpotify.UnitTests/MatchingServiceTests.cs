using RockBandSpotify.Models;
using RockBandSpotify.Services;

namespace RockBandSpotify.UnitTests;

/// <summary>
/// Covers which path each song takes — a known id or a search — since that is
/// what the catalogue's Spotify ids exist to change.
/// </summary>
public class MatchingServiceTests
{
    private sealed class FakeLookup : ITrackLookup
    {
        public Dictionary<string, SpotifyTrack> Known { get; init; } = new();
        public List<SpotifyTrack> SearchResult { get; set; } = new();
        public List<string> Searched { get; } = new();
        public List<string> LookedUp { get; } = new();
        public Exception? LookupThrows { get; set; }

        public Task<Dictionary<string, SpotifyTrack>> GetTracksAsync(IReadOnlyList<string> ids)
        {
            LookedUp.AddRange(ids);
            if (LookupThrows is not null) throw LookupThrows;
            return Task.FromResult(ids.Where(Known.ContainsKey).ToDictionary(i => i, i => Known[i]));
        }

        public Task<List<SpotifyTrack>> SearchTracksAsync(string title, string artist, int limit = 5)
        {
            Searched.Add(title);
            return Task.FromResult(SearchResult);
        }
    }

    private static SpotifyTrack Track(string id, string name, string artist) => new()
    {
        Id = id,
        Name = name,
        Artists = [new SpotifyArtist { Name = artist }],
    };

    private static CatalogueSong Song(string title, string artist, string? spotifyId = null) =>
        new() { Song = title, Artist = artist, SpotifyId = spotifyId };

    [Fact]
    public async Task A_song_with_a_known_id_is_never_searched()
    {
        var track = Track("abc", "Believer", "Imagine Dragons");
        var api = new FakeLookup { Known = { ["abc"] = track } };

        var results = await new MatchingService(api).MatchAllAsync(
            [Song("Believer", "Imagine Dragons", "abc")]);

        Assert.Empty(api.Searched);
        var match = Assert.Single(results);
        Assert.Equal(MatchStatus.Matched, match.Status);
        Assert.Same(track, match.Selected);
    }

    [Fact]
    public async Task A_known_id_is_certain_and_included()
    {
        var api = new FakeLookup { Known = { ["abc"] = Track("abc", "Believer", "Imagine Dragons") } };

        var match = Assert.Single(await new MatchingService(api)
            .MatchAllAsync([Song("Believer", "Imagine Dragons", "abc")]));

        // Nothing was guessed, so there is no doubt to express and nothing for a
        // person to weigh — one candidate, full confidence, included.
        Assert.Equal(1, match.Confidence);
        Assert.True(match.Include);
        Assert.Single(match.Candidates);
    }

    [Fact]
    public async Task A_song_without_an_id_is_searched_and_scored()
    {
        var api = new FakeLookup { SearchResult = [Track("xyz", "Believer", "Imagine Dragons")] };

        var match = Assert.Single(await new MatchingService(api)
            .MatchAllAsync([Song("Believer", "Imagine Dragons")]));

        Assert.Equal(["Believer"], api.Searched);
        Assert.Equal(MatchStatus.Matched, match.Status);
        Assert.Equal(1, match.Confidence, precision: 5);
    }

    [Fact]
    public async Task An_id_Spotify_no_longer_knows_falls_back_to_searching()
    {
        // GetTracksAsync omits ids it can't resolve rather than failing, so the
        // song has to reach the search path anyway.
        var api = new FakeLookup { SearchResult = [Track("xyz", "Believer", "Imagine Dragons")] };

        var match = Assert.Single(await new MatchingService(api)
            .MatchAllAsync([Song("Believer", "Imagine Dragons", "gone")]));

        Assert.Equal(["Believer"], api.Searched);
        Assert.Equal("xyz", match.Selected!.Id);
    }

    [Fact]
    public async Task A_failed_batch_lookup_degrades_to_searching_everything()
    {
        var api = new FakeLookup
        {
            LookupThrows = new HttpRequestException("network"),
            SearchResult = [Track("xyz", "Believer", "Imagine Dragons")],
        };

        var results = await new MatchingService(api).MatchAllAsync(
            [Song("Believer", "Imagine Dragons", "abc"), Song("Africa", "Toto", "def")]);

        Assert.Equal(2, api.Searched.Count);
        Assert.All(results, m => Assert.NotEqual(MatchStatus.Error, m.Status));
    }

    [Fact]
    public async Task Ids_are_looked_up_once_for_the_whole_library()
    {
        var api = new FakeLookup();
        await new MatchingService(api).MatchAllAsync(
            [Song("A", "X", "id1"), Song("B", "Y", "id2"), Song("C", "Z")]);

        // One batched call carrying both ids, rather than a request per song.
        Assert.Equal(["id1", "id2"], api.LookedUp);
    }

    [Fact]
    public async Task Progress_counts_every_song_whichever_path_it_took()
    {
        var api = new FakeLookup { Known = { ["abc"] = Track("abc", "A", "X") } };
        var seen = new List<int>();

        await new MatchingService(api).MatchAllAsync(
            [Song("A", "X", "abc"), Song("B", "Y")],
            (done, total) => { seen.Add(done); Assert.Equal(2, total); return Task.CompletedTask; });

        Assert.Equal([1, 2], seen);
    }

    [Fact]
    public async Task A_search_that_finds_nothing_is_left_out()
    {
        var api = new FakeLookup { SearchResult = [] };

        var match = Assert.Single(await new MatchingService(api)
            .MatchAllAsync([Song("Obscure B-Side", "Nobody")]));

        Assert.Equal(MatchStatus.NoResults, match.Status);
        Assert.False(match.Include);
    }
}
