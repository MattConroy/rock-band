using RockBandSpotify.Models;

namespace RockBandSpotify.UnitTests;

public class SongMatchTests
{
    private static SongMatch Match(MatchStatus status, bool include, SpotifyTrack? selected) => new()
    {
        Song = new RockBandSong { Title = "Believer", Artist = "Imagine Dragons" },
        Status = status,
        Include = include,
        Selected = selected,
    };

    private static readonly SpotifyTrack Track = new() { Id = "1", Name = "Believer" };

    [Fact]
    public void Syncable_when_matched_included_and_has_a_selection()
        => Assert.True(Match(MatchStatus.Matched, include: true, Track).IsSyncable);

    [Fact]
    public void Not_syncable_when_excluded()
        => Assert.False(Match(MatchStatus.Matched, include: false, Track).IsSyncable);

    [Fact]
    public void Not_syncable_without_a_selected_track()
        => Assert.False(Match(MatchStatus.Matched, include: true, selected: null).IsSyncable);

    [Theory]
    [InlineData(MatchStatus.Pending)]
    [InlineData(MatchStatus.NoResults)]
    [InlineData(MatchStatus.Error)]
    [InlineData(MatchStatus.Skipped)]
    public void Not_syncable_unless_status_is_matched(MatchStatus status)
        => Assert.False(Match(status, include: true, Track).IsSyncable);
}
