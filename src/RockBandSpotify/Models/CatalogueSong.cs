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
    /// The full games this song shipped in, <b>mainline first</b> — a game,
    /// spin-off, exclusive pack/expansion, era DLC bucket, or Rock Band Network.
    /// See <see cref="SourceCatalog"/>.
    /// <para>
    /// Nearly always one entry; 32 songs shipped in more than one game, so
    /// Everlong is <c>["RB2", "UNPLUGGED"]</c>. Index 0 is the mainline game, or
    /// the origin when no mainline shipped it — use <see cref="Primary"/> rather
    /// than indexing directly.
    /// </para>
    /// <para>
    /// This is membership, not playability: exports let most songs be played in
    /// later games without appearing in their tracklists.
    /// </para>
    /// </summary>
    [JsonPropertyName("sources")]
    public List<string> Sources { get; set; } = [];

    /// <summary>
    /// The song's mainline game, or its origin when no mainline shipped it.
    /// Null only if it somehow has no sources at all.
    /// </summary>
    [JsonIgnore]
    public string? Primary => Sources.Count > 0 ? Sources[0] : null;

    /// <summary>
    /// PlayStation Store content codes for this song — the last segment of a PSN
    /// product id, e.g. <c>RBPHOTOGRCCF04AD</c>.
    /// <para>
    /// Region-independent: a US listing reads <c>UP0006-…-RBPHOTOGRCCF04AD</c> and
    /// a European account's entitlement reads <c>EP0006-…-RBPHOTOGRCCF04AD</c>, so
    /// only this segment is stored and it matches either.
    /// </para>
    /// <para>
    /// Every code that grants this song, not only its own listing. Standalone and
    /// in-pack are different products; a song with no listing of its own carries
    /// whatever does grant it, which for an older disc is its game's export
    /// (<c>RBRB1DISCEXP2462</c> on all 47 exportable Rock Band 1 tracks) and for a
    /// game that was never exported is the game itself
    /// (<c>ROCKBAND4PS4000E</c> on all 65 Rock Band 4 tracks).
    /// Empty for songs no product grants: delisted Rock Band Network songs,
    /// delisted DLC, Beatles content, and the handful of disc tracks whose licence
    /// kept them out of the export.
    /// </para>
    /// </summary>
    [JsonPropertyName("psnIds")]
    public List<string> PsnIds { get; set; } = [];

    [JsonPropertyName("releaseDate")]
    public DateOnly? ReleaseDate { get; set; }
}
