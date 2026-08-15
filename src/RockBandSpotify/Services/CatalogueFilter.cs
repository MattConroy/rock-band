using RockBandSpotify.Models;

namespace RockBandSpotify.Services;

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
        string origin,
        bool selectedOnly,
        ISet<int> selectedIds)
    {
        IEnumerable<CatalogueSong> q = songs;

        if (search.Length > 0)
            q = q.Where(s => s.Song.Contains(search, StringComparison.OrdinalIgnoreCase)
                           || s.Artist.Contains(search, StringComparison.OrdinalIgnoreCase));

        if (genre.Length > 0)
            q = q.Where(s => s.Genre == genre);

        if (origin.Length > 0)
            q = q.Where(s => s.Origin == origin);

        if (selectedOnly)
            q = q.Where(s => selectedIds.Contains(s.Id));

        return q.ToList();
    }
}
