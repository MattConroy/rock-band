using RockBandSpotify.Models;

namespace RockBandSpotify.Services;

/// <param name="Matched">Songs the codes grant, in the order the codes name them.</param>
/// <param name="Unmatched">Codes that grant nothing in the catalogue.</param>
public sealed record OwnedSongs(
    IReadOnlyList<CatalogueSong> Matched,
    IReadOnlyList<string> Unmatched);

/// <summary>
/// Works out which catalogue songs an account owns, by comparing the content
/// codes PSN reports against the store ids in <see cref="CatalogueSong.PlayStationIds"/>.
///
/// <para>
/// A plain string comparison, deliberately. The codes are the same identifiers
/// the catalogue already stores, so there is nothing to infer.
/// </para>
/// <para>
/// One code can grant many songs: a disc export's songs have no store listings
/// of their own, so every one of them carries the export's code. The reverse
/// holds too — a song sold both standalone and in a pack carries both codes —
/// so each side of the comparison is deduplicated.
/// </para>
/// </summary>
public static class EntitlementResolver
{
    public static OwnedSongs Resolve(
        IEnumerable<string> codes,
        IReadOnlyList<CatalogueSong> catalogue)
    {
        var byPlayStationId = new Dictionary<string, List<CatalogueSong>>(StringComparer.OrdinalIgnoreCase);
        foreach (var song in catalogue)
            foreach (var id in song.PlayStationIds)
            {
                if (!byPlayStationId.TryGetValue(id, out var granted))
                    byPlayStationId[id] = granted = [];
                granted.Add(song);
            }

        var matched = new List<CatalogueSong>();
        var unmatched = new List<string>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenSongs = new HashSet<int>();

        foreach (var code in codes)
        {
            if (string.IsNullOrWhiteSpace(code) || !seenCodes.Add(code)) continue;

            if (byPlayStationId.TryGetValue(code, out var granted))
            {
                foreach (var song in granted)
                    if (seenSongs.Add(song.Id)) matched.Add(song);
            }
            else
            {
                unmatched.Add(code);
            }
        }

        return new OwnedSongs(matched, unmatched);
    }
}
