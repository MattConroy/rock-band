using Microsoft.AspNetCore.Components;
using RockBandSpotify.Models;
using RockBandSpotify.Services;

namespace RockBandSpotify.Pages;

/// <summary>
/// Orchestrates the four-step workflow: it holds view state and delegates the
/// actual work to the injected services, composing the step components in the
/// markup. No business logic lives here beyond wiring.
/// </summary>
public partial class Home
{
    [Inject] private SpotifyAuthService Auth { get; set; } = default!;
    [Inject] private SpotifyApiService Api { get; set; } = default!;
    [Inject] private PsnService Psn { get; set; } = default!;
    [Inject] private MatchingService Matcher { get; set; } = default!;
    [Inject] private PlaylistSyncService Sync { get; set; } = default!;
    [Inject] private PlaylistConfig PlaylistCfg { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    // Spotify
    private bool _signedIn;
    private SpotifyUser? _spotifyUser;
    private string? SpotifyUserName => _spotifyUser?.DisplayName ?? _spotifyUser?.Id;

    // PlayStation
    private bool _psnConnected;
    private bool _fetching;
    private string? _psnError;
    private SongLibrary? _library;

    // Matching
    private bool _matching;
    private int _progressDone;
    private int _progressTotal;
    private List<SongMatch> _matches = new();

    // Sync
    private bool _syncing;
    private SyncResult? _syncResult;
    private string? _error;

    private bool CanMatch => _signedIn && _library is { Songs.Count: > 0 };
    private int SelectedCount => _matches.Count(m => m.IsSyncable);

    protected override async Task OnInitializedAsync()
    {
        // Complete a Spotify login redirect if we just came back from one.
        await Auth.TryCompleteLoginAsync(Nav.Uri);
        _signedIn = await Auth.IsAuthenticatedAsync();
        if (_signedIn)
        {
            try { _spotifyUser = await Api.GetCurrentUserAsync(); }
            catch { /* token may have been revoked; ignore */ }
        }

        _psnConnected = await Psn.HasTokenAsync();
        if (_psnConnected)
            _library = await Psn.GetCachedSongsAsync();
    }

    private Task LoginSpotifyAsync() => Auth.BeginLoginAsync();

    private async Task LogoutSpotifyAsync()
    {
        await Auth.LogoutAsync();
        _signedIn = false;
        _spotifyUser = null;
    }

    private async Task ConnectPsnAsync(string npsso)
    {
        await Psn.SaveTokenAsync(npsso);
        _psnConnected = true;
        await FetchSongsAsync();
    }

    private async Task FetchSongsAsync()
    {
        _fetching = true;
        _psnError = null;
        try
        {
            _library = await Psn.FetchSongsAsync();
            if (_library.Songs.Count == 0)
                _psnError = "Connected, but no Rock Band songs were found. You may need to tune the gateway's filter (see gateway README).";
        }
        catch (Exception ex)
        {
            _psnError = ex.Message;
        }
        finally
        {
            _fetching = false;
        }
    }

    private async Task DisconnectPsnAsync()
    {
        await Psn.DisconnectAsync();
        _psnConnected = false;
        _library = null;
        _matches = new();
    }

    private async Task MatchAsync()
    {
        if (_library is null)
            return;

        _matching = true;
        _matches = new();
        _syncResult = null;
        _error = null;
        _progressDone = 0;
        _progressTotal = _library.Songs.Count;

        try
        {
            _matches = await Matcher.MatchAllAsync(_library.Songs, async (done, total) =>
            {
                _progressDone = done;
                _progressTotal = total;
                StateHasChanged();
                await Task.Yield();
            });
        }
        catch (Exception ex)
        {
            _error = $"Matching failed: {ex.Message}";
        }
        finally
        {
            _matching = false;
        }
    }

    private async Task SyncAsync()
    {
        _syncing = true;
        _error = null;
        _syncResult = null;
        try
        {
            _syncResult = await Sync.SyncAsync(_matches);
        }
        catch (Exception ex)
        {
            _error = $"Sync failed: {ex.Message}";
        }
        finally
        {
            _syncing = false;
        }
    }
}
