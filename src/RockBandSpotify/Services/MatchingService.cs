using RockBandSpotify.Models;

namespace RockBandSpotify.Services;

/// <summary>
/// Turns owned songs into Spotify tracks, ready for review before syncing.
///
/// <para>
/// Most songs need nothing from Spotify at all. The catalogue already knows
/// which track they are, and a track's URI follows from its id, so those are
/// resolved here with no request and nothing to score — nothing was guessed.
/// </para>
/// <para>
/// The remainder are searched by name and scored only if
/// <see cref="SpotifyConfig.SearchForMissingTracks"/> allows it; otherwise they
/// are left alone. Searching is where covers, live cuts and remasters mislead,
/// and where a long library burns through a rate limit.
/// </para>
/// </summary>
public class MatchingService
{
    private readonly ITrackLookup _api;
    private readonly SpotifyConfig _configuration;

    public MatchingService(ITrackLookup api, SpotifyConfig configuration)
    {
        _api = api;
        _configuration = configuration;
    }

    /// <summary>
    /// Matches every song, invoking <paramref name="onProgress"/> after each so the
    /// UI can update. Searching runs sequentially to stay well within Spotify's
    /// rate limits.
    /// </summary>
    public async Task<List<SongMatch>> MatchAllAsync(
        IEnumerable<CatalogueSong> songs,
        Func<int, int, Task>? onProgress = null)
    {
        var list = songs.ToList();
        var results = new List<SongMatch>(list.Count);

        for (var i = 0; i < list.Count; i++)
        {
            var song = list[i];
            var match = new SongMatch { Song = song };

            if (!string.IsNullOrEmpty(song.SpotifyId))
                Accept(match, TrackFor(song));
            else if (_configuration.SearchForMissingTracks)
                await SearchAndScoreAsync(song, match);
            else
                Skip(match);

            results.Add(match);
            if (onProgress is not null)
                await onProgress(i + 1, list.Count);
        }

        return results;
    }

    /// <summary>
    /// The track a catalogue id stands for, built rather than fetched.
    ///
    /// <para>
    /// This used to come from GET /tracks, which Spotify removed in February
    /// 2026 with no replacement. Every batch then failed, every song fell
    /// through to the search path, and with searching off the sync produced an
    /// empty playlist. Nothing was lost by dropping it: a URI follows from the
    /// id, and the title and artist shown are the catalogue's own — which is
    /// what they always were on screen anyway.
    /// </para>
    /// </summary>
    private static SpotifyTrack TrackFor(CatalogueSong song) => new()
    {
        Id = song.SpotifyId!,
        Uri = $"spotify:track:{song.SpotifyId}",
        Name = song.Song,
        Artists = [new SpotifyArtist { Name = song.Artist }],
    };

    /// <summary>A track the catalogue named: no candidates to weigh, no doubt to record.</summary>
    private static void Accept(SongMatch match, SpotifyTrack track)
    {
        match.Candidates = [track];
        match.Selected = track;
        match.Confidence = 1;
        match.Status = MatchStatus.Matched;
        match.Include = true;
    }

    /// <summary>
    /// Left alone: the catalogue has no track for this song and searching is
    /// switched off, so nothing was asked of Spotify and nothing is claimed.
    /// </summary>
    private static void Skip(SongMatch match)
    {
        match.Status = MatchStatus.Skipped;
        match.Include = false;
    }

    private async Task SearchAndScoreAsync(CatalogueSong song, SongMatch match)
    {
        try
        {
            var candidates = await _api.SearchTracksAsync(song.Song, song.Artist);
            if (candidates.Count == 0)
            {
                match.Candidates = candidates;
                match.Status = MatchStatus.NoResults;
                match.Include = false;
                return;
            }

            var scored = candidates
                .Select(c => (Track: c, Score: Score(song, c)))
                .OrderByDescending(x => x.Score)
                .ToList();
            match.Candidates = scored.Select(x => x.Track).ToList();
            match.Selected = scored[0].Track;
            match.Confidence = scored[0].Score;
            match.Status = MatchStatus.Matched;
            // Low-confidence matches are excluded by default so a bad guess
            // never silently lands in the playlist.
            match.Include = scored[0].Score >= 0.5;
        }
        catch (Exception ex)
        {
            match.Status = MatchStatus.Error;
            match.Error = ex.Message;
            match.Include = false;
        }
    }

    /// <summary>Cheap 0..1 similarity from normalized title + artist overlap.</summary>
    internal static double Score(CatalogueSong song, SpotifyTrack track)
    {
        var titleScore = Similarity(Normalize(song.Song), Normalize(track.Name));
        var artistScore = track.Artists
            .Select(a => Similarity(Normalize(song.Artist), Normalize(a.Name)))
            .DefaultIfEmpty(0)
            .Max();
        return (titleScore * 0.6) + (artistScore * 0.4);
    }

    internal static string Normalize(string value)
    {
        var chars = value.ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c == ' ')
            .ToArray();
        var cleaned = new string(chars);
        // Drop noise words that Rock Band metadata often adds.
        foreach (var noise in new[] { " remastered", " remaster", " single version", " album version", " live" })
            cleaned = cleaned.Replace(noise, "");
        return cleaned.Trim();
    }

    internal static double Similarity(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0)
            return 0;
        if (a == b)
            return 1;

        var setA = a.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var setB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        if (setA.Count == 0 || setB.Count == 0)
            return 0;

        var intersection = setA.Intersect(setB).Count();
        var union = setA.Union(setB).Count();
        return (double)intersection / union; // Jaccard token overlap.
    }
}
