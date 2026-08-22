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

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt.AddSeconds(-60);
}

public class SpotifyUser
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }
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
public class SpotifyTracksResponse
{
    [JsonPropertyName("tracks")]
    public List<SpotifyTrack?> Tracks { get; set; } = new();
}

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

    [JsonIgnore]
    public string? WebUrl => ExternalUrls.TryGetValue("spotify", out var u) ? u : null;
}

public class SpotifyPlaylistPage
{
    [JsonPropertyName("items")]
    public List<SpotifyPlaylist> Items { get; set; } = new();

    [JsonPropertyName("next")]
    public string? Next { get; set; }
}
