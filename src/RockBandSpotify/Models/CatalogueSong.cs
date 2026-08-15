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

    /// <summary>Where the song originates: RB1-4 on-disc, DLC era, or a spin-off game.</summary>
    [JsonPropertyName("origin")]
    public string? Origin { get; set; }

    [JsonPropertyName("rb12")]
    public string? Rb12 { get; set; }

    [JsonPropertyName("rb3")]
    public string? Rb3 { get; set; }

    /// <summary>Availability on Rock Band 4: "Yes", "Import Only", "No", etc.</summary>
    [JsonPropertyName("rb4")]
    public string? Rb4 { get; set; }

    [JsonPropertyName("other")]
    public string? Other { get; set; }
}
