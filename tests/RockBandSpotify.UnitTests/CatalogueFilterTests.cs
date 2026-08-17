using RockBandSpotify.Models;
using RockBandSpotify.Services;

namespace RockBandSpotify.UnitTests;

public class CatalogueFilterTests
{
    private static readonly List<CatalogueSong> Songs = new()
    {
        new() { Id = 1, Song = "Believer", Artist = "Imagine Dragons", Genre = "Alternative", Sources = ["RB4_DLC"] },
        new() { Id = 2, Song = "Africa", Artist = "Toto", Genre = "Pop-Rock", Sources = ["RB4_DLC"] },
        new() { Id = 3, Song = "Africa", Artist = "Weezer", Genre = "Alternative", Sources = ["RB4_DLC"] },
        new() { Id = 4, Song = "Paranoid", Artist = "Black Sabbath", Genre = "Metal", Sources = ["RB1"] },
        // Shipped in two full games — Everlong's real shape.
        new() { Id = 5, Song = "Everlong", Artist = "Foo Fighters", Genre = "Alternative", Sources = ["RB2", "UNPLUGGED"] },
    };

    private static List<CatalogueSong> Apply(string search = "", string genre = "", string source = "")
        => CatalogueFilter.Apply(Songs, search, genre, source);

    [Fact]
    public void No_filters_returns_everything()
        => Assert.Equal(5, Apply().Count);

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
        => Assert.Equal(new[] { 1, 3, 5 }, Apply(genre: "Alternative").Select(s => s.Id));

    [Fact]
    public void Source_filter_is_exact_match()
        => Assert.Equal(new[] { 4 }, Apply(source: "RB1").Select(s => s.Id));

    [Fact]
    public void Source_filter_matches_a_songs_origin()
        => Assert.Equal(new[] { 5 }, Apply(source: "RB2").Select(s => s.Id));

    [Fact]
    public void Source_filter_also_matches_a_non_origin_source()
        => Assert.Equal(new[] { 5 }, Apply(source: "UNPLUGGED").Select(s => s.Id));

    [Fact]
    public void Source_filter_ignores_a_game_the_song_only_resembles()
        => Assert.Empty(Apply(source: "RB3"));

    [Fact]
    public void Filters_combine_with_AND()
        => Assert.Equal(new[] { 3 }, Apply(search: "africa", genre: "Alternative").Select(s => s.Id));
}
