using RockBandSpotify.Models;

namespace RockBandSpotify.Services;

/// <summary>
/// The part of Spotify the matcher needs, kept narrow so it can be faked in
/// tests without standing up the whole API client.
///
/// <para>
/// Only searching lives here. Songs the catalogue already knows the track for
/// need nothing from Spotify — the URI follows from the id — so the matcher
/// resolves those itself.
/// </para>
/// </summary>
public interface ITrackLookup
{
    /// <summary>Candidate tracks for a title and artist, best guess first.</summary>
    Task<List<SpotifyTrack>> SearchTracksAsync(string title, string artist, int limit = 5);
}
