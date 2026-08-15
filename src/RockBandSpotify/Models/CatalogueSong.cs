using System.Text.Json.Serialization;

namespace RockBandSpotify.Models;

/// <summary>
/// One entry from the full official Rock Band song catalogue
/// (wwwroot/data/catalogue.json), independent of what anyone owns.
/// </summary>
public class CatalogueSong
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("song")]
    public string Song { get; set; } = "";

    [JsonPropertyName("artist")]
    public string Artist { get; set; } = "";

    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("genre")]
    public string? Genre { get; set; }

    /// <summary>How the song was obtainable: on-disc game, spin-off, exclusive
    /// pack/expansion, era DLC, or Rock Band Network. See <see cref="SourceCatalog"/>.</summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("releaseDate")]
    public DateOnly? ReleaseDate { get; set; }
}
