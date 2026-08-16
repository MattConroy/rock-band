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
            // Origin first, so a multi-source song stays with the game it came
            // from rather than sorting under its second game. Then the whole cell
            // text, so rows within one origin group are ordered by what the user
            // can actually see — without it the 32 multi-source songs sit at
            // arbitrary positions among their single-source siblings and the
            // column reads as unsorted.
            "Source" => asc
                ? songs.OrderBy(s => SourceCatalog.Name(s.Primary), StringComparer.OrdinalIgnoreCase)
                       .ThenBy(s => SourceCatalog.Names(s.Sources), StringComparer.OrdinalIgnoreCase)
                : songs.OrderByDescending(s => SourceCatalog.Name(s.Primary), StringComparer.OrdinalIgnoreCase)
                       .ThenByDescending(s => SourceCatalog.Names(s.Sources), StringComparer.OrdinalIgnoreCase),
            _ => throw new ArgumentOutOfRangeException(nameof(column), column, "Unknown sort column"),
        };

        return ordered.ToList();
    }
}
