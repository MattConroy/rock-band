using RockBandSpotify.Models;

namespace RockBandSpotify.Services;

public enum SortDirection { None, Ascending, Descending }

/// <summary>
/// Pure sorting logic for the catalogue browser, split out of the Catalogue
/// page so it's testable without spinning up a component.
/// </summary>
public static class CatalogueSort
{
    public static List<CatalogueSong> Apply(IEnumerable<CatalogueSong> songs, string? column, SortDirection direction)
    {
        if (column is null || direction == SortDirection.None)
            return songs.ToList();

        bool asc = direction == SortDirection.Ascending;

        IOrderedEnumerable<CatalogueSong> ordered = column switch
        {
            "Song" => asc
                ? songs.OrderBy(s => s.Song, StringComparer.OrdinalIgnoreCase)
                : songs.OrderByDescending(s => s.Song, StringComparer.OrdinalIgnoreCase),
            "Artist" => asc
                ? songs.OrderBy(s => s.Artist, StringComparer.OrdinalIgnoreCase)
                : songs.OrderByDescending(s => s.Artist, StringComparer.OrdinalIgnoreCase),
            "Year" => asc
                ? songs.OrderBy(s => s.Year)
                : songs.OrderByDescending(s => s.Year),
            "Genre" => asc
                ? songs.OrderBy(s => s.Genre, StringComparer.OrdinalIgnoreCase)
                : songs.OrderByDescending(s => s.Genre, StringComparer.OrdinalIgnoreCase),
            // Sorts on the origin alone. A song's extra sources are shown in the
            // cell but must not affect its position, or the 32 multi-source songs
            // would scatter away from the game they came from.
            "Source" => asc
                ? songs.OrderBy(s => SourceCatalog.Name(s.Primary), StringComparer.OrdinalIgnoreCase)
                : songs.OrderByDescending(s => SourceCatalog.Name(s.Primary), StringComparer.OrdinalIgnoreCase),
            _ => throw new ArgumentOutOfRangeException(nameof(column), column, "Unknown sort column"),
        };

        return ordered.ToList();
    }
}
