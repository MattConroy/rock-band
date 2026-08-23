using RockBandSpotify.Models;
using RockBandSpotify.Services;

namespace RockBandSpotify.UnitTests;

/// <summary>
/// What the sync says it did. Every one of these is a success, and they need
/// telling apart — "added 832 songs" and "added nothing, none of them are on
/// Spotify" are very different outcomes to be handed the same silence for.
/// </summary>
public class SyncDescriptionTests
{
    private static SyncResult Result(int added, int already, bool created = false) =>
        new(new SpotifyPlaylist { Name = "Rock Band DLC" }, added, already, created);

    [Fact]
    public void Creating_a_playlist_and_filling_it_says_both()
    {
        var text = ConnectionState.Describe(Result(added: 832, already: 0, created: true));

        Assert.Contains("Created", text);
        Assert.Contains("832 songs", text);
        Assert.Contains("Rock Band DLC", text);
    }

    [Fact]
    public void Adding_to_an_existing_playlist_says_updated()
    {
        var text = ConnectionState.Describe(Result(added: 5, already: 100));

        Assert.Contains("Updated", text);
        Assert.Contains("added 5 songs", text);
        Assert.Contains("100 were already there", text);
    }

    [Fact]
    public void A_rerun_that_changes_nothing_says_so()
    {
        var text = ConnectionState.Describe(Result(added: 0, already: 832));

        Assert.Contains("already up to date", text);
        Assert.Contains("832", text);
    }

    [Fact]
    public void An_empty_playlist_explains_itself_rather_than_claiming_success()
    {
        // The failure that started this: a playlist created with nothing in it,
        // and nothing on screen to say why.
        var text = ConnectionState.Describe(Result(added: 0, already: 0, created: true));

        Assert.Contains("Nothing to add", text);
        Assert.Contains("no", text.ToLowerInvariant());
    }

    [Fact]
    public void One_song_is_not_called_songs()
        => Assert.Contains("added 1 song.", ConnectionState.Describe(Result(added: 1, already: 0)));
}
