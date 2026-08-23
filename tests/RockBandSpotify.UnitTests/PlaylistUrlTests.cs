using RockBandSpotify.Models;

namespace RockBandSpotify.UnitTests;

/// <summary>
/// The playlist's address, which the header stores and the Open button uses.
/// </summary>
public class PlaylistUrlTests
{
    [Fact]
    public void The_url_Spotify_supplies_is_used_as_given()
    {
        var playlist = new SpotifyPlaylist
        {
            Id = "abc",
            ExternalUrls = { ["spotify"] = "https://open.spotify.com/playlist/supplied" },
        };

        Assert.Equal("https://open.spotify.com/playlist/supplied", playlist.WebUrl);
    }

    [Fact]
    public void Without_one_the_address_is_built_from_the_id()
    {
        // The address follows from the id, so a response missing external_urls
        // shouldn't leave an Open button that goes nowhere.
        Assert.Equal("https://open.spotify.com/playlist/abc", new SpotifyPlaylist { Id = "abc" }.WebUrl);
    }

    [Fact]
    public void An_empty_url_is_treated_as_missing()
        => Assert.Equal("https://open.spotify.com/playlist/abc",
            new SpotifyPlaylist { Id = "abc", ExternalUrls = { ["spotify"] = "" } }.WebUrl);
}
