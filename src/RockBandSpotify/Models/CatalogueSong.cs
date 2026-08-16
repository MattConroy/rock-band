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

    /// <summary>
    /// The full games this song shipped in, <b>origin first</b> — a game,
    /// spin-off, exclusive pack/expansion, era DLC bucket, or Rock Band Network.
    /// See <see cref="SourceCatalog"/>.
    /// <para>
    /// Nearly always one entry; 32 songs shipped in more than one game, so
    /// Everlong is <c>["RB2", "UNPLUGGED"]</c>. Index 0 is the origin — use
    /// <see cref="Primary"/> rather than indexing directly.
    /// </para>
    /// <para>
    /// This is membership, not playability: exports let most songs be played in
    /// later games without appearing in their tracklists.
    /// </para>
    /// </summary>
    [JsonPropertyName("sources")]
    public List<string> Sources { get; set; } = [];

    /// <summary>Where the song first appeared, or null if it has no sources.</summary>
    [JsonIgnore]
    public string? Primary => Sources.Count > 0 ? Sources[0] : null;

    [JsonPropertyName("releaseDate")]
    public DateOnly? ReleaseDate { get; set; }
}
