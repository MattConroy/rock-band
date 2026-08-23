using System.Text.Json.Serialization;

namespace RockBandSpotify.Models;

public class SpotifyTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}

/// <summary>Token material persisted to localStorage between page loads.</summary>
public class StoredToken
{
    public string AccessToken { get; set; } = "";
    public string? RefreshToken { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// What the token was granted for. Kept so that adding a scope to the app
    /// invalidates tokens issued before it: Spotify answers a call the token
    /// doesn't cover with a bare 403, which is indistinguishable from a real
    /// permission problem, so the mismatch is better caught here.
    /// </summary>
    public string? Scope { get; set; }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt.AddSeconds(-60);

    /// <summary>Whether this token covers every scope in <paramref name="required"/>.</summary>
    public bool Covers(string required)
    {
        var granted = Split(Scope);
        return Split(required).All(granted.Contains);
    }

    private static HashSet<string> Split(string? scopes) =>
        new((scopes ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
}


public class SpotifyTrack
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("uri")]
    public string Uri { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("artists")]
    public List<SpotifyArtist> Artists { get; set; } = new();

    [JsonIgnore]
    public string ArtistNames => string.Join(", ", Artists.Select(a => a.Name));
}

public class SpotifyArtist
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}



public class SpotifySearchResponse
{
    [JsonPropertyName("tracks")]
    public SpotifyTrackPage? Tracks { get; set; }
}

/// <summary>GET /v1/tracks — entries are null where an id no longer resolves.</summary>

public class SpotifyTrackPage
{
    [JsonPropertyName("items")]
    public List<SpotifyTrack> Items { get; set; } = new();
}

public class SpotifyPlaylist
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("uri")]
    public string Uri { get; set; } = "";

    [JsonPropertyName("external_urls")]
    public Dictionary<string, string> ExternalUrls { get; set; } = new();

    /// <summary>
    /// Where to open the playlist. Spotify normally supplies this, but the
    /// address follows from the id, so a response without external_urls
    /// doesn't have to mean a button that goes nowhere.
    /// </summary>
    [JsonIgnore]
    public string WebUrl => ExternalUrls.TryGetValue("spotify", out var u) && !string.IsNullOrEmpty(u)
        ? u
        : $"https://open.spotify.com/playlist/{Id}";
}

public class SpotifyPlaylistPage
{
    [JsonPropertyName("items")]
    public List<SpotifyPlaylist> Items { get; set; } = new();

    [JsonPropertyName("next")]
    public string? Next { get; set; }
}
