using System.Text.Json;
using Microsoft.JSInterop;
using RockBandSpotify.Models;

namespace RockBandSpotify.Services;

/// <summary>How far along a connection is, which decides what pressing it does.</summary>
/// <summary>Whether a notice reports something that worked or something that didn't.</summary>
public enum NoticeKind { Good, Bad }

/// <summary>Something worth telling the person who pressed the button.</summary>
public record Notice(string Text, NoticeKind Kind);

/// <summary>
/// The playlist as it stood after the last sync. The count is remembered so
/// the header can say how much is in there without asking Spotify again —
/// and so a playlist that came back empty is visible as empty.
/// </summary>
public record PlaylistInfo(string Url, string Name, int TrackCount);

public enum ConnectionStatus
{
    /// <summary>Not signed in.</summary>
    Disconnected,

    /// <summary>Signed in, but nothing has been fetched or written yet.</summary>
    Connected,

    /// <summary>Songs fetched from PlayStation, or a playlist written to Spotify.</summary>
    Synced,
}

/// <summary>
/// What the two connection buttons in the header show and do.
///
/// <para>
/// Both connections follow the same three steps, so one place decides what a
/// press means: sign in, fetch or sync, and once that is done, something useful
/// with the result. Held apart from the buttons themselves because the state
/// outlives any one page — the header is always on screen, and the catalogue
/// needs to know what is owned.
/// </para>
/// </summary>
public class ConnectionState
{
    private const string PlaylistKey = "rock_band_spotify_playlist";

    private readonly SpotifyAuthenticationService _authentication;
    private readonly SpotifyApiService _api;
    private readonly PlayStationService _playStation;
    private readonly PlaylistSyncService _sync;
    private readonly IJSRuntime _javaScript;
    private readonly string _playlistName;

    public ConnectionState(
        SpotifyAuthenticationService authentication,
        SpotifyApiService api,
        PlayStationService playStation,
        PlaylistSyncService sync,
        PlaylistConfig playlist,
        IJSRuntime javaScript)
    {
        _playlistName = playlist.Name;
        _authentication = authentication;
        _api = api;
        _playStation = playStation;
        _sync = sync;
        _javaScript = javaScript;
    }

    /// <summary>Raised whenever anything below changes, so the header can redraw.</summary>
    public event Action? Changed;

    public ConnectionStatus PlayStation { get; private set; }
    public ConnectionStatus Spotify { get; private set; }

    /// <summary>How many catalogue songs the account owns, once fetched.</summary>
    public int OwnedCount { get; private set; }

    /// <summary>
    /// How many of those the catalogue already knows a Spotify track for. The
    /// rest can't reach a playlist, so it is the number worth showing next to
    /// the owned count rather than leaving the shortfall to be discovered at
    /// the end of a sync.
    /// </summary>
    public int MatchedCount { get; private set; }

    /// <summary>The synced playlist, or null if there isn't one yet.</summary>
    public PlaylistInfo? Playlist { get; private set; }

    public string? PlaylistUrl => Playlist?.Url;

    /// <summary>The name the playlist would be given, whether or not it exists.</summary>
    public string PlaylistName => Playlist?.Name ?? _playlistName;

    /// <summary>Set while a press is being acted on, so the button can show it.</summary>
    public bool PlayStationBusy { get; private set; }
    public bool SpotifyBusy { get; private set; }

    /// <summary>
    /// What just happened, good or bad. Successes are worth saying out loud —
    /// a sync that adds nothing and a sync that adds eight hundred songs
    /// otherwise look identical from the outside.
    /// </summary>
    public Notice? Notice { get; private set; }

    /// <summary>The last failure, for the sign-in dialog to show inline.</summary>
    public string? Error => Notice is { Kind: NoticeKind.Bad } bad ? bad.Text : null;

    /// <summary>What the buttons are doing, shown while they do it.</summary>
    public string? BusyText { get; private set; }

    public bool IsSpotifyConfigured => _authentication.IsConfigured;
    public bool IsPlayStationConfigured => _playStation.IsGatewayConfigured;

    /// <summary>Reads both connections back from the browser on start-up.</summary>
    public async Task RefreshAsync()
    {
        var library = await _playStation.GetCachedSongsAsync();
        Count(library);
        PlayStation = OwnedCount > 0 ? ConnectionStatus.Synced
            : await _playStation.HasTokenAsync() ? ConnectionStatus.Connected
            : ConnectionStatus.Disconnected;

        Playlist = await ReadPlaylistAsync();
        Spotify = !await _authentication.IsAuthenticatedAsync() ? ConnectionStatus.Disconnected
            : Playlist is not null ? ConnectionStatus.Synced
            : ConnectionStatus.Connected;

        Notify();
    }

    /// <summary>Stores the pasted token and immediately fetches, so one paste is enough.</summary>
    public async Task ConnectPlayStationAsync(string npsso)
    {
        try
        {
            await _playStation.SaveTokenAsync(npsso);
        }
        catch (Exception ex)
        {
            Fail($"Couldn't save the token: {ex.Message}");
            return;
        }

        PlayStation = ConnectionStatus.Connected;
        Notify();
        await FetchPlayStationAsync();
    }

    /// <summary>Asks PlayStation what the account owns and resolves it to catalogue songs.</summary>
    public async Task FetchPlayStationAsync()
    {
        PlayStationBusy = true;
        BusyText = "Asking PlayStation what you own…";
        Notice = null;
        Notify();
        try
        {
            var library = await _playStation.FetchSongsAsync();
            Count(library);
            PlayStation = OwnedCount > 0 ? ConnectionStatus.Synced : ConnectionStatus.Connected;
            if (OwnedCount == 0)
                Fail("PlayStation returned nothing this app recognises as a Rock Band song.");
            else
                Succeed($"Found {OwnedCount} songs you own — {MatchedCount} of them are on Spotify.");
        }
        catch (Exception ex)
        {
            Fail(ex.Message);
        }
        finally
        {
            PlayStationBusy = false;
            BusyText = null;
            Notify();
        }
    }

    public async Task DisconnectPlayStationAsync()
    {
        await _playStation.DisconnectAsync();
        PlayStation = ConnectionStatus.Disconnected;
        Count(null);
        Succeed("Disconnected from PlayStation.");
    }

    /// <summary>
    /// Forgets the fetched songs but keeps the token, so the next press
    /// re-fetches without another trip to Sony's login page.
    /// </summary>
    public async Task ClearPlayStationSongsAsync()
    {
        await _playStation.ClearSongsAsync();
        Count(null);
        PlayStation = ConnectionStatus.Connected;
        Succeed("Cleared the fetched songs. Your sign-in is still here.");
    }

    private void Count(SongLibrary? library)
    {
        OwnedCount = library?.Songs.Count ?? 0;
        MatchedCount = library?.Songs.Count(s => !string.IsNullOrEmpty(s.SpotifyId)) ?? 0;
    }

    /// <summary>
    /// Sends the browser off to Spotify. Wrapped because a failure here — a
    /// redirect URI the app isn't registered for, storage the browser won't
    /// write — otherwise leaves the press looking like it did nothing.
    /// </summary>
    public async Task SignInToSpotifyAsync()
    {
        Notice = null;
        try
        {
            await _authentication.BeginLoginAsync();
        }
        catch (Exception ex)
        {
            Fail($"Couldn't start the Spotify sign-in: {ex.Message}");
        }
    }

    /// <summary>
    /// Matches the owned songs and writes them to the playlist. Both halves in
    /// one press: nearly every song resolves from an id the catalogue already
    /// holds, so there is no longer a list of guesses worth stopping to review.
    /// </summary>
    public async Task SyncSpotifyAsync()
    {
        SpotifyBusy = true;
        BusyText = $"Adding songs to {_playlistName}…";
        Notice = null;
        Notify();
        try
        {
            var library = await _playStation.GetCachedSongsAsync();
            if (library is null || library.Songs.Count == 0)
            {
                Fail("Connect PlayStation first — there are no owned songs to sync.");
                return;
            }

            BusyText = $"Adding songs to {_playlistName}…";
            Notify();
            var result = await _sync.SyncAsync(library.Songs);

            Playlist = new PlaylistInfo(
                result.Playlist.WebUrl,
                result.Playlist.Name,
                result.Added + result.AlreadyPresent);
            await WritePlaylistAsync(Playlist);
            Spotify = ConnectionStatus.Synced;
            Succeed(Describe(result));
        }
        catch (Exception ex)
        {
            Fail(ex.Message);
        }
        finally
        {
            SpotifyBusy = false;
            BusyText = null;
            Notify();
        }
    }

    public async Task DisconnectSpotifyAsync()
    {
        await _authentication.LogoutAsync();
        await WritePlaylistAsync(null);
        Playlist = null;
        Spotify = ConnectionStatus.Disconnected;
        Succeed("Disconnected from Spotify.");
    }

    public void ClearNotice()
    {
        Notice = null;
        Notify();
    }

    /// <summary>
    /// Says what the sync actually did. "Nothing to add" and "added eight
    /// hundred songs" are both successes, and they need telling apart.
    /// </summary>
    internal static string Describe(SyncResult result)
    {
        var name = result.Playlist.Name;
        if (result.Added == 0)
            return result.AlreadyPresent > 0
                ? $"{name} was already up to date — all {result.AlreadyPresent} songs were in it."
                : $"Nothing to add to {name}. None of your owned songs have a Spotify track yet.";

        var verb = result.Created ? "Created" : "Updated";
        var song = result.Added == 1 ? "song" : "songs";
        return result.AlreadyPresent > 0
            ? $"{verb} {name} — added {result.Added} {song}, {result.AlreadyPresent} were already there."
            : $"{verb} {name} — added {result.Added} {song}.";
    }

    private void Succeed(string text)
    {
        Notice = new Notice(text, NoticeKind.Good);
        Notify();
    }

    private void Fail(string text)
    {
        Notice = new Notice(text, NoticeKind.Bad);
        Notify();
    }

    private void Notify() => Changed?.Invoke();

    private async Task<PlaylistInfo?> ReadPlaylistAsync()
    {
        try
        {
            var raw = await _javaScript.InvokeAsync<string?>("rockBandSpotify.getItem", PlaylistKey);
            if (string.IsNullOrEmpty(raw)) return null;

            // Earlier builds stored just the URL. Read those rather than
            // throwing away a sync that already happened.
            if (raw.TrimStart().StartsWith('"'))
            {
                var url = JsonSerializer.Deserialize<string>(raw);
                return url is null ? null : new PlaylistInfo(url, _playlistName, 0);
            }

            return JsonSerializer.Deserialize<PlaylistInfo>(raw);
        }
        catch
        {
            return null;
        }
    }

    private async Task WritePlaylistAsync(PlaylistInfo? playlist)
    {
        try
        {
            if (playlist is null)
                await _javaScript.InvokeVoidAsync("rockBandSpotify.removeItem", PlaylistKey);
            else
                await _javaScript.InvokeVoidAsync("rockBandSpotify.setItem", PlaylistKey, JsonSerializer.Serialize(playlist));
        }
        catch { /* the synced state just won't survive a reload */ }
    }
}
