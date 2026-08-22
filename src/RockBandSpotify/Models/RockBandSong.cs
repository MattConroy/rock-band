using System.Text.Json.Serialization;

namespace RockBandSpotify.Models;

/// <summary>
/// A single owned Rock Band song, as produced by the PSN refresh workflow
/// and stored in wwwroot/data/songs.json.
/// </summary>
public class RockBandSong
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("artist")]
    public string Artist { get; set; } = "";

    /// <summary>Raw product/entitlement name from PSN, kept for debugging matches.</summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    /// <summary>PSN product id / entitlement id, when available.</summary>
    [JsonPropertyName("productId")]
    public string? ProductId { get; set; }

    /// <summary>The song's known Spotify track id, when the catalogue has one.</summary>
    [JsonPropertyName("spotifyTrackId")]
    public string? SpotifyTrackId { get; set; }

    /// <summary>ISO date this entry was purchased/added, when available.</summary>
    [JsonPropertyName("purchasedAt")]
    public string? PurchasedAt { get; set; }

    /// <summary>Stable key used for de-duplication and match caching.</summary>
    [JsonIgnore]
    public string Key => $"{Artist}{Title}".ToLowerInvariant();
}

/// <summary>Envelope written by the refresh workflow so the app can show freshness.</summary>
public class SongLibrary
{
    [JsonPropertyName("generatedAt")]
    public string? GeneratedAt { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("songs")]
    public List<RockBandSong> Songs { get; set; } = new();
}
