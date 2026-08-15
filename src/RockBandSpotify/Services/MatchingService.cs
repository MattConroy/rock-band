using RockBandSpotify.Models;

namespace RockBandSpotify.Services;

/// <summary>
/// Searches Spotify for each owned song and scores candidates so the review
/// table can highlight confident matches vs. ones that need a human look.
/// </summary>
public class MatchingService
{
    private readonly SpotifyApiService _api;

    public MatchingService(SpotifyApiService api)
    {
        _api = api;
    }

    /// <summary>
    /// Matches every song, invoking <paramref name="onProgress"/> after each so the
    /// UI can update. Runs sequentially to stay well within Spotify rate limits.
    /// </summary>
    public async Task<List<SongMatch>> MatchAllAsync(
        IEnumerable<RockBandSong> songs,
        Func<int, int, Task>? onProgress = null)
    {
        var list = songs.ToList();
        var results = new List<SongMatch>(list.Count);

        for (var i = 0; i < list.Count; i++)
        {
            var match = new SongMatch { Song = list[i] };
            try
            {
                var candidates = await _api.SearchTracksAsync(list[i].Title, list[i].Artist);
                match.Candidates = candidates;
                if (candidates.Count == 0)
                {
                    match.Status = MatchStatus.NoResults;
                    match.Include = false;
                }
                else
                {
                    var scored = candidates
                        .Select(c => (Track: c, Score: Score(list[i], c)))
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
            }
            catch (Exception ex)
            {
                match.Status = MatchStatus.Error;
                match.Error = ex.Message;
                match.Include = false;
            }

            results.Add(match);
            if (onProgress is not null)
                await onProgress(i + 1, list.Count);
        }

        return results;
    }

    /// <summary>Cheap 0..1 similarity from normalized title + artist overlap.</summary>
    internal static double Score(RockBandSong song, SpotifyTrack track)
    {
        var titleScore = Similarity(Normalize(song.Title), Normalize(track.Name));
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
