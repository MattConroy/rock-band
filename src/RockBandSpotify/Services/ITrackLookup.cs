using RockBandSpotify.Models;

namespace RockBandSpotify.Services;

/// <summary>
/// The two ways of turning a song into a Spotify track: looking one up by an id
/// we already hold, or searching for it by name.
///
/// <para>
/// Narrowed to these from the wider API surface so the matching logic can be
/// exercised without a Spotify account or a network.
/// </para>
/// </summary>
public interface ITrackLookup
{
    /// <summary>Tracks by id, in batches. Ids Spotify doesn't recognise are absent.</summary>
    Task<Dictionary<string, SpotifyTrack>> GetTracksAsync(IReadOnlyList<string> ids);

    /// <summary>Candidate tracks for a title and artist, best guess first.</summary>
    Task<List<SpotifyTrack>> SearchTracksAsync(string title, string artist, int limit = 5);
}
