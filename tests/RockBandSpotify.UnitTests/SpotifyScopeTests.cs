using System.Text.Json;

namespace RockBandSpotify.UnitTests;

/// <summary>
/// The shipped scopes have to cover every call the sync makes. Getting this
/// wrong costs a 403 that only shows up against a real account, so it is
/// pinned here instead.
/// </summary>
public class SpotifyScopeTests
{
    private static string ShippedScopes()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "wwwroot", "appsettings.json");
        if (!File.Exists(path))
            path = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "../../../../../src/RockBandSpotify/wwwroot/appsettings.json"));

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty("Spotify").GetProperty("Scopes").GetString()!;
    }

    [Theory]
    // Reading /me/playlists to find an existing playlist.
    [InlineData("playlist-read-private")]
    // Creating the playlist and adding tracks, whichever visibility is configured.
    [InlineData("playlist-modify-private")]
    [InlineData("playlist-modify-public")]
    public void The_shipped_scopes_cover_every_call_the_sync_makes(string scope)
        => Assert.Contains(scope, ShippedScopes().Split(' '));
}
