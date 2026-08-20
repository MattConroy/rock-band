using RockBandSpotify.Models;

namespace RockBandSpotify.Services;

/// <param name="Matched">Codes that are a known store id, with the song they identify.</param>
/// <param name="Unmatched">Codes not in the catalogue.</param>
public sealed record OwnedSongs(
    IReadOnlyList<CatalogueSong> Matched,
    IReadOnlyList<string> Unmatched);

/// <summary>
/// Works out which catalogue songs an account owns, by comparing the content
/// codes PSN reports against the store ids in <see cref="CatalogueSong.PsnIds"/>.
///
/// <para>
/// A plain string comparison, deliberately. The codes are the same identifiers
/// the catalogue already stores, so there is nothing to infer.
/// </para>
/// </summary>
public static class EntitlementResolver
{
    public static OwnedSongs Resolve(
        IEnumerable<string> codes,
        IReadOnlyList<CatalogueSong> catalogue)
    {
        var byPsnId = new Dictionary<string, CatalogueSong>(StringComparer.OrdinalIgnoreCase);
        foreach (var song in catalogue)
            foreach (var id in song.PsnIds)
                byPsnId.TryAdd(id, song);

        var matched = new List<CatalogueSong>();
        var unmatched = new List<string>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenSongs = new HashSet<int>();

        foreach (var code in codes)
        {
            if (string.IsNullOrWhiteSpace(code) || !seenCodes.Add(code)) continue;

            if (byPsnId.TryGetValue(code, out var song))
            {
                // Two codes can name the same song — standalone and in a pack.
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
