using RockBandSpotify.Models;

namespace RockBandSpotify.Services;

public record SyncResult(
    SpotifyPlaylist Playlist,
    int Added,
    int AlreadyPresent,
    bool Created);

/// <summary>
/// Finds-or-creates the target playlist and adds any matched tracks that aren't
/// already in it (so re-runs are additive, never duplicating).
/// </summary>
public class PlaylistSyncService
{
    private readonly SpotifyApiService _api;
    private readonly PlaylistConfig _config;

    public PlaylistSyncService(SpotifyApiService api, PlaylistConfig config)
    {
        _api = api;
        _config = config;
    }

    public async Task<SyncResult> SyncAsync(IEnumerable<SongMatch> matches)
    {
        var uris = matches
            .Where(m => m.IsSyncable)
            .Select(m => m.Selected!.Uri)
            .Distinct()
            .ToList();

        var playlist = await _api.FindPlaylistByNameAsync(_config.Name);
        var created = false;
        if (playlist is null)
        {
            playlist = await _api.CreatePlaylistAsync(
                _config.Name, _config.Description, _config.Public);
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
