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
        public List<SpotifyTrack> SearchResult { get; set; } = new();
        public List<string> Searched { get; } = new();

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

    /// <summary>Searching is off by default, so tests that exercise it opt in.</summary>
    private static MatchingService Matcher(ITrackLookup api, bool search = true) =>
        new(api, new SpotifyConfig { SearchForMissingTracks = search });

    [Fact]
    public async Task A_song_with_a_known_id_is_never_searched()
    {
        var api = new FakeLookup();

        var results = await Matcher(api).MatchAllAsync(
            [Song("Believer", "Imagine Dragons", "abc")]);

        Assert.Empty(api.Searched);
        var match = Assert.Single(results);
        Assert.Equal(MatchStatus.Matched, match.Status);
        Assert.Equal("abc", match.Selected!.Id);
    }

    [Fact]
    public async Task A_known_id_becomes_a_track_uri_without_asking_Spotify()
    {
        // GET /tracks was removed in February 2026, and turning an id into a
        // URI never needed it — the form is fixed.
        var match = Assert.Single(await Matcher(new FakeLookup())
            .MatchAllAsync([Song("Believer", "Imagine Dragons", "abc")]));

        Assert.Equal("spotify:track:abc", match.Selected!.Uri);
        Assert.True(match.IsSyncable);
    }

    [Fact]
    public async Task A_known_song_carries_the_catalogue_title_and_artist()
    {
        var match = Assert.Single(await Matcher(new FakeLookup())
            .MatchAllAsync([Song("Believer", "Imagine Dragons", "abc")]));

        Assert.Equal("Believer", match.Selected!.Name);
        Assert.Equal("Imagine Dragons", match.Selected.ArtistNames);
    }

    [Fact]
    public async Task A_known_id_is_certain_and_included()
    {
        var api = new FakeLookup();

        var match = Assert.Single(await Matcher(api)
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

        var match = Assert.Single(await Matcher(api)
            .MatchAllAsync([Song("Believer", "Imagine Dragons")]));

        Assert.Equal(["Believer"], api.Searched);
        Assert.Equal(MatchStatus.Matched, match.Status);
        Assert.Equal(1, match.Confidence, precision: 5);
    }

    [Fact]
    public async Task Progress_counts_every_song_whichever_path_it_took()
    {
        var api = new FakeLookup();
        var seen = new List<int>();

        await Matcher(api).MatchAllAsync(
            [Song("A", "X", "abc"), Song("B", "Y")],
            (done, total) => { seen.Add(done); Assert.Equal(2, total); return Task.CompletedTask; });

        Assert.Equal([1, 2], seen);
    }

    [Fact]
    public async Task With_searching_off_an_unknown_song_is_left_alone()
    {
        var api = new FakeLookup { SearchResult = [Track("xyz", "Believer", "Imagine Dragons")] };

        var match = Assert.Single(await Matcher(api, search: false)
            .MatchAllAsync([Song("Believer", "Imagine Dragons")]));

        Assert.Empty(api.Searched);
        Assert.Equal(MatchStatus.Skipped, match.Status);
        Assert.False(match.Include);
        Assert.Null(match.Selected);
    }

    [Fact]
    public async Task Switching_searching_off_does_not_affect_known_songs()
    {
        var api = new FakeLookup();

        var results = await Matcher(api, search: false).MatchAllAsync(
            [Song("Believer", "Imagine Dragons", "abc"), Song("Africa", "Toto")]);

        Assert.Empty(api.Searched);
        Assert.Equal(MatchStatus.Matched, results[0].Status);
        Assert.Equal("spotify:track:abc", results[0].Selected!.Uri);
        Assert.Equal(MatchStatus.Skipped, results[1].Status);
    }

    [Fact]
    public async Task A_library_of_known_songs_asks_Spotify_for_nothing()
    {
        // The whole matching step is now offline when the catalogue knows the
        // tracks, which is the case for nearly every owned song.
        var api = new FakeLookup();

        var results = await Matcher(api, search: false).MatchAllAsync(
            [Song("A", "X", "id1"), Song("B", "Y", "id2"), Song("C", "Z", "id3")]);

        Assert.Empty(api.Searched);
        Assert.All(results, m => Assert.True(m.IsSyncable));
    }

    [Fact]
    public async Task A_search_that_finds_nothing_is_left_out()
    {
        var api = new FakeLookup { SearchResult = [] };

        var match = Assert.Single(await Matcher(api)
            .MatchAllAsync([Song("Obscure B-Side", "Nobody")]));

        Assert.Equal(MatchStatus.NoResults, match.Status);
        Assert.False(match.Include);
    }
}
