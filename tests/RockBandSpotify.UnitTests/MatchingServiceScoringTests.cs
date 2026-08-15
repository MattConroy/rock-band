using RockBandSpotify.Models;
using RockBandSpotify.Services;

namespace RockBandSpotify.UnitTests;

public class MatchingServiceScoringTests
{
    private static RockBandSong Song(string title, string artist) => new() { Title = title, Artist = artist };

    private static SpotifyTrack Track(string name, params string[] artists) => new()
    {
        Name = name,
        Artists = artists.Select(a => new SpotifyArtist { Name = a }).ToList(),
    };

    [Fact]
    public void Exact_title_and_artist_scores_1()
    {
        var score = MatchingService.Score(Song("Believer", "Imagine Dragons"), Track("Believer", "Imagine Dragons"));
        Assert.Equal(1.0, score, precision: 5);
    }

    [Fact]
    public void Completely_unrelated_track_scores_0()
    {
        var score = MatchingService.Score(Song("Believer", "Imagine Dragons"), Track("Africa", "Toto"));
        Assert.Equal(0.0, score, precision: 5);
    }

    [Fact]
    public void Case_and_punctuation_differences_still_match()
    {
        var score = MatchingService.Score(
            Song("(Don't Fear) The Reaper", "Blue Öyster Cult"),
            Track("Dont Fear The Reaper", "Blue Öyster Cult"));
        Assert.True(score > 0.9, $"expected high score, got {score}");
    }

    [Theory]
    [InlineData("Believer - Remastered", "believer")]
    [InlineData("Believer (Live)", "believer")]
    [InlineData("Believer - Single Version", "believer")]
    [InlineData("Believer - Album Version", "believer")]
    public void Normalize_strips_common_release_noise(string input, string expected)
        => Assert.Equal(expected, MatchingService.Normalize(input));

    [Fact]
    public void Title_carries_more_weight_than_artist()
    {
        // Right title, wrong artist vs. wrong title, right artist — title should win.
        var titleMatch = MatchingService.Score(Song("Believer", "Imagine Dragons"), Track("Believer", "Nobody"));
        var artistMatch = MatchingService.Score(Song("Believer", "Imagine Dragons"), Track("Nothing", "Imagine Dragons"));
        Assert.True(titleMatch > artistMatch, $"title match {titleMatch} should exceed artist match {artistMatch}");
    }

    [Fact]
    public void Multiple_track_artists_take_the_best_match()
    {
        var score = MatchingService.Score(
            Song("Believer", "Imagine Dragons"),
            Track("Believer", "Some Other Artist", "Imagine Dragons"));
        Assert.Equal(1.0, score, precision: 5);
    }

    [Theory]
    [InlineData("", "anything")]
    [InlineData("anything", "")]
    public void Similarity_with_an_empty_string_is_0(string a, string b)
        => Assert.Equal(0, MatchingService.Similarity(a, b));
}
