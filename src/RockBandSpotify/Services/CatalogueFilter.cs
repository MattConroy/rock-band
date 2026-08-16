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
        string source)
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

        return q.ToList();
    }
}
