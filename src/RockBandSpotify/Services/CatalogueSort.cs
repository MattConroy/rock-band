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
            "Source" => asc
                ? songs.OrderBy(s => s, SourceOrder.Instance)
                : songs.OrderByDescending(s => s, SourceOrder.Instance),
            _ => throw new ArgumentOutOfRangeException(nameof(column), column, "Unknown sort column"),
        };

        return ordered.ToList();
    }

    /// <summary>
    /// Orders songs by their <see cref="CatalogueSong.Sources"/> in the order the
    /// data holds them: first source, then second, and so on — so a Rock Band 2
    /// song sorts on "Rock Band 2" before Unplugged is even considered.
    /// <para>
    /// Comparing element by element rather than on the joined cell text is what
    /// keeps a game's songs together. "Rock Band 2 DLC" would otherwise fall
    /// between "Rock Band 2" and "Rock Band 2 · Rock Band Unplugged", because the
    /// separator sorts after a letter, splitting the Rock Band 2 group in half.
    /// </para>
    /// <para>
    /// Where one list is a prefix of the other the shorter comes first, so a song
    /// that only shipped in Rock Band 2 precedes one that also shipped elsewhere.
    /// </para>
    /// </summary>
    private sealed class SourceOrder : IComparer<CatalogueSong>
    {
        internal static readonly SourceOrder Instance = new();

        public int Compare(CatalogueSong? x, CatalogueSong? y)
        {
            IReadOnlyList<string> a = x?.Sources ?? [];
            IReadOnlyList<string> b = y?.Sources ?? [];

            for (var i = 0; i < Math.Min(a.Count, b.Count); i++)
            {
                var byName = string.Compare(
                    SourceCatalog.Name(a[i]), SourceCatalog.Name(b[i]), StringComparison.OrdinalIgnoreCase);
                if (byName != 0) return byName;
            }

            return a.Count.CompareTo(b.Count);
        }
    }
}
