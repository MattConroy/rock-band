using RockBandSpotify.Models;
using RockBandSpotify.Services;

namespace RockBandSpotify.UnitTests;

public class CatalogueSortTests
{
    private static readonly List<CatalogueSong> Songs = new()
    {
        new() { Id = 1, Song = "Believer", Artist = "Imagine Dragons", Year = 2017, Genre = "Alternative", Sources = ["RB4_DLC"] },
        new() { Id = 2, Song = "Africa", Artist = "Toto", Year = 1982, Genre = "Pop-Rock", Sources = ["RB4_DLC"] },
        new() { Id = 3, Song = "africa", Artist = "Weezer", Year = null, Genre = "Alternative", Sources = ["RB1"] },
        new() { Id = 4, Song = "Paranoid", Artist = "Black Sabbath", Year = 1970, Genre = "Metal", Sources = ["RB1"] },
    };

    private static List<CatalogueSong> Apply(string? column, SortDirection direction)
        => CatalogueSort.Apply(Songs, column, direction);

    [Fact]
    public void No_column_returns_the_original_order()
        => Assert.Equal(new[] { 1, 2, 3, 4 }, Apply(null, SortDirection.Ascending).Select(s => s.Id));

    [Fact]
    public void None_direction_returns_the_original_order_even_with_a_column_set()
        => Assert.Equal(new[] { 1, 2, 3, 4 }, Apply("Song", SortDirection.None).Select(s => s.Id));

    [Fact]
    public void Song_ascending_is_case_insensitive()
        => Assert.Equal(new[] { 2, 3, 1, 4 }, Apply("Song", SortDirection.Ascending).Select(s => s.Id));

    [Fact]
    public void Song_descending_reverses_it()
        => Assert.Equal(new[] { 4, 1, 2, 3 }, Apply("Song", SortDirection.Descending).Select(s => s.Id));

    [Fact]
    public void Artist_ascending_sorts_by_artist_name()
        => Assert.Equal(new[] { 4, 1, 2, 3 }, Apply("Artist", SortDirection.Ascending).Select(s => s.Id));

    [Fact]
    public void Year_ascending_puts_nulls_first()
        => Assert.Equal(new[] { 3, 4, 2, 1 }, Apply("Year", SortDirection.Ascending).Select(s => s.Id));

    [Fact]
    public void Year_descending_puts_nulls_last()
        => Assert.Equal(new[] { 1, 2, 4, 3 }, Apply("Year", SortDirection.Descending).Select(s => s.Id));

    [Fact]
    public void Genre_ascending_sorts_alphabetically()
        => Assert.Equal(new[] { 1, 3, 4, 2 }, Apply("Genre", SortDirection.Ascending).Select(s => s.Id));

    [Fact]
    public void Source_sorts_by_display_name_not_raw_code()
    {
        // "Rock Band 1" (RB1) sorts before "Rock Band 4 DLC" (RB4_DLC) by name.
        var result = Apply("Source", SortDirection.Ascending).Select(s => s.Id).ToList();
        Assert.Equal(new[] { 3, 4, 1, 2 }, result);
    }

    [Fact]
    public void Source_groups_by_origin_before_other_sources()
    {
        // Both RB2 songs stay together and ahead of the RB4 one, even though
        // "Rock Band 2 · Rock Band Unplugged" would sort after "Rock Band 4 DLC"
        // on cell text alone.
        var songs = new List<CatalogueSong>
        {
            new() { Id = 1, Song = "Zzz", Artist = "A", Sources = ["RB4_DLC"] },
            new() { Id = 2, Song = "Everlong", Artist = "Foo Fighters", Sources = ["RB2", "UNPLUGGED"] },
            new() { Id = 3, Song = "Aaa", Artist = "B", Sources = ["RB2"] },
        };
        var result = CatalogueSort.Apply(songs, "Source", SortDirection.Ascending).Select(s => s.Id);
        Assert.Equal(new[] { 3, 2, 1 }, result);
    }

    [Fact]
    public void Source_orders_within_an_origin_group_by_the_visible_cell_text()
    {
        // All four share an origin, so only the extra sources distinguish them.
        // They must come out in the order the cell renders, not input order.
        var songs = new List<CatalogueSong>
        {
            new() { Id = 1, Song = "A", Artist = "A", Sources = ["RB2", "UNPLUGGED"] },
            new() { Id = 2, Song = "B", Artist = "B", Sources = ["RB2"] },
            new() { Id = 3, Song = "C", Artist = "C", Sources = ["RB2", "BLITZ"] },
            new() { Id = 4, Song = "D", Artist = "D", Sources = ["RB2"] },
        };
        var result = CatalogueSort.Apply(songs, "Source", SortDirection.Ascending).ToList();

        // "Rock Band 2" < "Rock Band 2 · Rock Band Blitz" < "… · Rock Band Unplugged"
        Assert.Equal(new[] { 2, 4, 3, 1 }, result.Select(s => s.Id));
        // and the rendered column is genuinely non-decreasing
        var cells = result.Select(s => SourceCatalog.Names(s.Sources)).ToList();
        Assert.Equal(cells.OrderBy(c => c, StringComparer.OrdinalIgnoreCase), cells);
    }

    [Fact]
    public void Unknown_column_throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => Apply("NotAColumn", SortDirection.Ascending));
}
