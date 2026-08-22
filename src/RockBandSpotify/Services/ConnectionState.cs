using System.Text.Json;
using Microsoft.JSInterop;
using RockBandSpotify.Models;

namespace RockBandSpotify.Services;

/// <summary>How far along a connection is, which decides what pressing it does.</summary>
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
    private const string PlaylistKey = "rb_spotify_playlist";

    private readonly SpotifyAuthService _auth;
    private readonly SpotifyApiService _api;
    private readonly PsnService _psn;
    private readonly MatchingService _matcher;
    private readonly PlaylistSyncService _sync;
    private readonly IJSRuntime _js;

    public ConnectionState(
        SpotifyAuthService auth,
        SpotifyApiService api,
        PsnService psn,
        MatchingService matcher,
        PlaylistSyncService sync,
        IJSRuntime js)
    {
        _auth = auth;
        _api = api;
        _psn = psn;
        _matcher = matcher;
        _sync = sync;
        _js = js;
    }

    /// <summary>Raised whenever anything below changes, so the header can redraw.</summary>
    public event Action? Changed;

    public ConnectionStatus Psn { get; private set; }
    public ConnectionStatus Spotify { get; private set; }

    /// <summary>How many catalogue songs the account owns, once fetched.</summary>
    public int OwnedCount { get; private set; }

    /// <summary>Where the synced playlist lives, for opening it.</summary>
    public string? PlaylistUrl { get; private set; }

    /// <summary>Set while a press is being acted on, so the button can show it.</summary>
    public bool PsnBusy { get; private set; }
    public bool SpotifyBusy { get; private set; }

    /// <summary>Whatever went wrong last, for the header to surface.</summary>
    public string? Error { get; private set; }

    public bool IsSpotifyConfigured => _auth.IsConfigured;
    public bool IsPsnConfigured => _psn.IsGatewayConfigured;

    /// <summary>Reads both connections back from the browser on start-up.</summary>
    public async Task RefreshAsync()
    {
        var library = await _psn.GetCachedSongsAsync();
        OwnedCount = library?.Songs.Count ?? 0;
        Psn = OwnedCount > 0 ? ConnectionStatus.Synced
            : await _psn.HasTokenAsync() ? ConnectionStatus.Connected
            : ConnectionStatus.Disconnected;

        PlaylistUrl = await ReadPlaylistUrlAsync();
        Spotify = !await _auth.IsAuthenticatedAsync() ? ConnectionStatus.Disconnected
            : PlaylistUrl is not null ? ConnectionStatus.Synced
            : ConnectionStatus.Connected;

        Notify();
    }

    /// <summary>Stores the pasted token and immediately fetches, so one paste is enough.</summary>
    public async Task ConnectPsnAsync(string npsso)
    {
        await _psn.SaveTokenAsync(npsso);
        Psn = ConnectionStatus.Connected;
        Notify();
        await FetchPsnAsync();
    }

    /// <summary>Asks PlayStation what the account owns and resolves it to catalogue songs.</summary>
    public async Task FetchPsnAsync()
    {
        PsnBusy = true;
        Error = null;
        Notify();
        try
        {
            var library = await _psn.FetchSongsAsync();
            OwnedCount = library.Songs.Count;
            Psn = OwnedCount > 0 ? ConnectionStatus.Synced : ConnectionStatus.Connected;
            if (OwnedCount == 0)
                Error = "PlayStation returned nothing this app recognises as a Rock Band song.";
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            PsnBusy = false;
            Notify();
        }
    }

    public async Task DisconnectPsnAsync()
    {
        await _psn.DisconnectAsync();
        Psn = ConnectionStatus.Disconnected;
        OwnedCount = 0;
        Notify();
    }

    public Task SignInToSpotifyAsync() => _auth.BeginLoginAsync();

    /// <summary>
    /// Matches the owned songs and writes them to the playlist. Both halves in
    /// one press: nearly every song resolves from an id the catalogue already
    /// holds, so there is no longer a list of guesses worth stopping to review.
    /// </summary>
    public async Task SyncSpotifyAsync()
    {
        SpotifyBusy = true;
        Error = null;
        Notify();
        try
        {
            var library = await _psn.GetCachedSongsAsync();
            if (library is null || library.Songs.Count == 0)
            {
                Error = "Connect PlayStation first — there are no owned songs to sync.";
                return;
            }

            var matches = await _matcher.MatchAllAsync(library.Songs);
            var result = await _sync.SyncAsync(matches);

            PlaylistUrl = result.Playlist.WebUrl;
            await WritePlaylistUrlAsync(PlaylistUrl);
            Spotify = ConnectionStatus.Synced;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            SpotifyBusy = false;
            Notify();
        }
    }

    public async Task DisconnectSpotifyAsync()
    {
        await _auth.LogoutAsync();
        await WritePlaylistUrlAsync(null);
        PlaylistUrl = null;
        Spotify = ConnectionStatus.Disconnected;
        Notify();
    }

    public void ClearError()
    {
        Error = null;
        Notify();
    }

    private void Notify() => Changed?.Invoke();

    private async Task<string?> ReadPlaylistUrlAsync()
    {
        try
        {
            var raw = await _js.InvokeAsync<string?>("rbSpotify.getItem", PlaylistKey);
            return string.IsNullOrEmpty(raw) ? null : JsonSerializer.Deserialize<string>(raw);
        }
        catch
        {
            return null;
        }
    }

    private async Task WritePlaylistUrlAsync(string? url)
    {
        try
        {
            if (url is null)
                await _js.InvokeVoidAsync("rbSpotify.removeItem", PlaylistKey);
            else
                await _js.InvokeVoidAsync("rbSpotify.setItem", PlaylistKey, JsonSerializer.Serialize(url));
        }
        catch { /* the synced state just won't survive a reload */ }
    }
}
