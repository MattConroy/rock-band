using System.Text.Json.Serialization;

namespace RockBandSpotify.Models;

/// <summary>
/// The songs an account owns, resolved against the catalogue.
///
/// <para>
/// Only the ids are persisted; the songs are rehydrated from the catalogue the
/// browser already holds. Keeping a second copy of each song's title and
/// artist would let it fall behind the catalogue, which is the one description
/// of what a song is.
/// </para>
/// </summary>
public class SongLibrary
{
    /// <summary>When PlayStation was last asked, as reported by the gateway.</summary>
    [JsonPropertyName("generatedAt")]
    public string? GeneratedAt { get; set; }

    [JsonPropertyName("songIds")]
    public List<int> SongIds { get; set; } = [];

    /// <summary>The catalogue rows those ids name. Rebuilt on load, never stored.</summary>
    [JsonIgnore]
    public List<CatalogueSong> Songs { get; set; } = [];
}
