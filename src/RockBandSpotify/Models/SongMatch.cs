namespace RockBandSpotify.Models;

public enum MatchStatus
{
    Pending,
    Matched,
    NoResults,
    Skipped,
    Error
}

/// <summary>
/// Pairs an owned catalogue song with its candidate Spotify tracks and the
/// user's chosen track. Drives the review table before syncing.
/// </summary>
public class SongMatch
{
    public required CatalogueSong Song { get; init; }

    public MatchStatus Status { get; set; } = MatchStatus.Pending;

    public List<SpotifyTrack> Candidates { get; set; } = new();

    /// <summary>The track that will actually be added to the playlist.</summary>
    public SpotifyTrack? Selected { get; set; }

    /// <summary>0..1 confidence for the top candidate, for surfacing shaky matches.</summary>
    public double Confidence { get; set; }

    public string? Error { get; set; }

    public bool Include { get; set; } = true;

    public bool IsSyncable => Include && Status == MatchStatus.Matched && Selected is not null;
}
