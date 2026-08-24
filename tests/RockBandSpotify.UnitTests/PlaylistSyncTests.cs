using RockBandSpotify.Models;
using RockBandSpotify.Services;

namespace RockBandSpotify.UnitTests;

/// <summary>
/// Turning owned songs into Spotify tracks. There is no searching or scoring
/// any more: the catalogue records which track a song is, and a URI follows
/// from its id.
/// </summary>
public class PlaylistSyncTests
{
    private static CatalogueSong Song(string title, string? spotifyId) =>
        new() { Song = title, Artist = "Someone", SpotifyId = spotifyId };

    [Fact]
    public void An_id_becomes_a_track_uri()
        => Assert.Equal(["spotify:track:abc"], PlaylistSyncService.TrackUris([Song("Believer", "abc")]));

    [Fact]
    public void A_song_with_no_id_contributes_nothing()
        => Assert.Empty(PlaylistSyncService.TrackUris([Song("Obscure B-Side", null)]));

    [Fact]
    public void An_empty_id_counts_as_no_id()
        => Assert.Empty(PlaylistSyncService.TrackUris([Song("Obscure B-Side", "  ")]));

    [Fact]
    public void One_track_named_by_two_songs_is_added_once()
    {
        // Rock Band ships re-recordings alongside originals, and both point at
        // the one studio track Spotify has.
        var uris = PlaylistSyncService.TrackUris(
            [Song("Tom Sawyer", "abc"), Song("Tom Sawyer (Original Version)", "abc")]);

        Assert.Equal(["spotify:track:abc"], uris);
    }

    [Fact]
    public void The_owned_songs_that_do_resolve_survive_the_ones_that_do_not()
    {
        var uris = PlaylistSyncService.TrackUris(
            [Song("A", "one"), Song("B", null), Song("C", "two")]);

        Assert.Equal(["spotify:track:one", "spotify:track:two"], uris);
    }
}
