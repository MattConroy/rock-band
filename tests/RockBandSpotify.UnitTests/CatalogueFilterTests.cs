using RockBandSpotify.Models;
using RockBandSpotify.Services;

namespace RockBandSpotify.UnitTests;

public class CatalogueFilterTests
{
    private static readonly List<CatalogueSong> Songs = new()
    {
        new() { Id = 1, Song = "Believer", Artist = "Imagine Dragons", Genre = "Alternative", Origin = "DLC4" },
        new() { Id = 2, Song = "Africa", Artist = "Toto", Genre = "Pop-Rock", Origin = "DLC4" },
        new() { Id = 3, Song = "Africa", Artist = "Weezer", Genre = "Alternative", Origin = "DLC4" },
        new() { Id = 4, Song = "Paranoid", Artist = "Black Sabbath", Genre = "Metal", Origin = "RB1" },
    };

    private static List<CatalogueSong> Apply(string search = "", string genre = "", string origin = "")
        => CatalogueFilter.Apply(Songs, search, genre, origin);

    [Fact]
    public void No_filters_returns_everything()
        => Assert.Equal(4, Apply().Count);

    [Fact]
    public void Search_matches_song_title_case_insensitively()
        => Assert.Equal(new[] { 1 }, Apply(search: "believ").Select(s => s.Id));

    [Fact]
    public void Search_matches_artist_name()
        => Assert.Equal(new[] { 2 }, Apply(search: "toto").Select(s => s.Id));

    [Fact]
    public void Search_can_match_multiple_songs_sharing_a_title()
        => Assert.Equal(new[] { 2, 3 }, Apply(search: "africa").Select(s => s.Id));

    [Fact]
    public void Genre_filter_is_exact_match()
        => Assert.Equal(new[] { 1, 3 }, Apply(genre: "Alternative").Select(s => s.Id));

    [Fact]
    public void Origin_filter_is_exact_match()
        => Assert.Equal(new[] { 4 }, Apply(origin: "RB1").Select(s => s.Id));

    [Fact]
    public void Filters_combine_with_AND()
        => Assert.Equal(new[] { 3 }, Apply(search: "africa", genre: "Alternative").Select(s => s.Id));
}
