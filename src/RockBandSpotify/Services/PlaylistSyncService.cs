using RockBandSpotify.Models;

namespace RockBandSpotify.Services;

public record SyncResult(
    SpotifyPlaylist Playlist,
    int Added,
    int AlreadyPresent,
    bool Created);

/// <summary>
/// Finds-or-creates the target playlist and adds any owned song the catalogue
/// knows a Spotify track for and the playlist doesn't already hold, so re-runs
/// are additive and never duplicate.
///
/// <para>
/// There is no matching step. The catalogue already records which track a song
/// is, and a track's URI follows from its id, so resolving an owned song is a
/// lookup rather than a guess. Songs the catalogue has no id for are simply
/// left out.
/// </para>
/// </summary>
public class PlaylistSyncService
{
    private readonly SpotifyApiService _api;
    private readonly PlaylistConfig _configuration;

    public PlaylistSyncService(SpotifyApiService api, PlaylistConfig configuration)
    {
        _api = api;
        _configuration = configuration;
    }

    /// <summary>
    /// The Spotify tracks an owned library resolves to. Songs the catalogue has
    /// no id for contribute nothing, and a track named by two songs — a disc
    /// version and its re-recording, say — contributes once.
    /// </summary>
    internal static List<string> TrackUris(IEnumerable<CatalogueSong> owned) => owned
        .Select(song => song.SpotifyId)
        .Where(id => !string.IsNullOrWhiteSpace(id))
        .Select(id => $"spotify:track:{id}")
        .Distinct(StringComparer.Ordinal)
        .ToList();

    public async Task<SyncResult> SyncAsync(IEnumerable<CatalogueSong> owned)
    {
        var uris = TrackUris(owned);

        var playlist = await _api.FindPlaylistByNameAsync(_configuration.Name);
        var created = false;
        if (playlist is null)
        {
            playlist = await _api.CreatePlaylistAsync(
                _configuration.Name, _configuration.Description, _configuration.Public);
            created = true;
        }

        var existing = created
            ? new HashSet<string>()
            : await _api.GetPlaylistTrackUrisAsync(playlist.Id);

        var toAdd = uris.Where(u => !existing.Contains(u)).ToList();
        var alreadyPresent = uris.Count - toAdd.Count;

        if (toAdd.Count > 0)
            await _api.AddTracksAsync(playlist.Id, toAdd);

        return new SyncResult(playlist, toAdd.Count, alreadyPresent, created);
    }
}
