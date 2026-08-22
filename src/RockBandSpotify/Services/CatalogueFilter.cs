using RockBandSpotify.Models;

namespace RockBandSpotify.Services;

/// <summary>Whether to narrow the catalogue to what the viewer does or doesn't own.</summary>
public enum OwnedFilter
{
    Any,
    Owned,
    NotOwned,
}

/// <summary>
/// Pure filtering logic for the catalogue browser, split out of the Catalogue
/// page so it's testable without spinning up a component.
/// </summary>
public static class CatalogueFilter
{
    public static List<CatalogueSong> Apply(
        IEnumerable<CatalogueSong> songs,
        string search,
        string genre,
        string source,
        OwnedFilter owned = OwnedFilter.Any,
        IReadOnlySet<int>? ownedIds = null)
    {
        IEnumerable<CatalogueSong> q = songs;

        if (search.Length > 0)
            q = q.Where(s => s.Song.Contains(search, StringComparison.OrdinalIgnoreCase)
                           || s.Artist.Contains(search, StringComparison.OrdinalIgnoreCase));

        if (genre.Length > 0)
            q = q.Where(s => s.Genre == genre);

        // Matches any of the song's sources, not just its origin — filtering to
        // "Rock Band Unplugged" should find Everlong even though it originated
        // on the Rock Band 2 disc.
        if (source.Length > 0)
            q = q.Where(s => s.Sources.Contains(source));

        // With nothing fetched from PlayStation the owned set is empty, and
        // "owned" would hide the whole catalogue — so the filter only bites
        // once there is something to compare against.
        if (owned != OwnedFilter.Any && ownedIds is { Count: > 0 })
            q = owned == OwnedFilter.Owned
                ? q.Where(s => ownedIds.Contains(s.Id))
                : q.Where(s => !ownedIds.Contains(s.Id));

        return q.ToList();
    }
}
