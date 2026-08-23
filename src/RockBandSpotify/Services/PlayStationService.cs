using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using RockBandSpotify.Models;

namespace RockBandSpotify.Services;

/// <summary>
/// Owns the PlayStation side: holds the pasted npsso token (cached in the
/// browser's localStorage), and fetches the owned-song list through the
/// stateless gateway Worker (the browser can't call PSN directly — no CORS).
/// The token is your own and never leaves your machine except to your Worker.
/// </summary>
public class PlayStationService
{
    private const string TokenKey = "rock_band_playstation_npsso";

    private readonly HttpClient _http;
    private readonly IJSRuntime _javaScript;
    private readonly PlayStationConfig _configuration;
    private readonly CatalogueService _catalogue;
    private readonly OwnedLibrary _owned;

    public PlayStationService(HttpClient http, IJSRuntime javaScript, PlayStationConfig configuration, CatalogueService catalogue, OwnedLibrary owned)
    {
        _http = http;
        _javaScript = javaScript;
        _configuration = configuration;
        _catalogue = catalogue;
        _owned = owned;
    }

    public bool IsGatewayConfigured => _configuration.IsConfigured;

    /// <summary>Link that opens Sony's login and lands on the page showing the npsso.</summary>
    public string SsoCookieUrl => "https://ca.account.sony.com/api/v1/ssocookie";

    public async Task<bool> HasTokenAsync()
        => !string.IsNullOrEmpty(await GetItemAsync(TokenKey));

    public async Task SaveTokenAsync(string npsso)
        => await SetItemAsync(TokenKey, npsso.Trim());

    public async Task DisconnectAsync()
    {
        await RemoveItemAsync(TokenKey);
        await _owned.ClearAsync();
    }

    /// <summary>Forgets the fetched songs but keeps the token.</summary>
    public Task ClearSongsAsync() => _owned.ClearAsync();

    /// <summary>The last fetched library, rebuilt from the stored ids, or null if there isn't one.</summary>
    public async Task<SongLibrary?> GetCachedSongsAsync()
    {
        var stored = await _owned.LoadAsync();
        if (stored is null || stored.SongIds.Count == 0) return null;
        return await RehydrateAsync(stored);
    }

    /// <summary>
    /// Calls the gateway with the stored npsso, caches the result, and returns it.
    /// Throws with a friendly message if not connected or the gateway rejects the token.
    /// </summary>
    public async Task<SongLibrary> FetchSongsAsync()
    {
        if (!IsGatewayConfigured)
            throw new InvalidOperationException("No PSN gateway URL is configured (see appsettings.json).");

        var npsso = await GetItemAsync(TokenKey);
        if (string.IsNullOrEmpty(npsso))
            throw new InvalidOperationException("Not connected to PlayStation — paste your token first.");

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync(_configuration.GatewayUrl, new { npsso });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Couldn't reach the PSN gateway: {ex.Message}");
        }

        if (!response.IsSuccessStatusCode)
        {
            var reason = await TryReadErrorAsync(response);
            throw new InvalidOperationException(reason
                ?? $"Gateway returned {(int)response.StatusCode}.");
        }

        // The gateway reports content codes, not names — deliberately, so it never
        // needs the catalogue. Turning them into songs is a straight comparison
        // against the store ids the catalogue already holds.
        var owned = await response.Content.ReadFromJsonAsync<PlayStationEntitlementsResponse>()
                    ?? new PlayStationEntitlementsResponse();

        var catalogue = await _catalogue.GetSongsAsync();
        var resolved = EntitlementResolver.Resolve(owned.Items.Select(i => i.Code), catalogue);

        var library = new SongLibrary
        {
            GeneratedAt = owned.GeneratedAt,
            SongIds = resolved.Matched.Select(s => s.Id).ToList(),
            Songs = resolved.Matched.OrderBy(s => s.Artist).ThenBy(s => s.Song).ToList(),
        };

        // One stored list, read by both the catalogue page's ownership column
        // and the matching flow, so neither has to ask PlayStation again.
        await _owned.SaveAsync(library);
        return library;
    }

    /// <summary>
    /// The gateway's debug endpoint, pretty-printed: the raw entitlement codes
    /// an account holds, for working out why an owned song isn't showing.
    /// </summary>
    public async Task<string> FetchRawDebugAsync()
    {
        var npsso = await GetItemAsync(TokenKey);
        if (string.IsNullOrEmpty(npsso))
            throw new InvalidOperationException("Not connected to PlayStation.");

        var url = _configuration.GatewayUrl.TrimEnd('/') + "/?debug=1";
        var response = await _http.PostAsJsonAsync(url, new { npsso });
        var body = await response.Content.ReadAsStringAsync();

        // Pretty-print if it's JSON, so it's readable on screen.
        try
        {
            using var doc = JsonDocument.Parse(body);
            return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return body;
        }
    }

    private static async Task<string?> TryReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("error", out var err))
                return err.GetString();
        }
        catch { /* non-JSON body */ }
        return null;
    }

    private async Task<SongLibrary> RehydrateAsync(SongLibrary stored)
    {
        var catalogue = await _catalogue.GetSongsAsync();
        var byId = catalogue.ToDictionary(s => s.Id);
        stored.Songs = stored.SongIds
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .OrderBy(s => s.Artist).ThenBy(s => s.Song)
            .ToList();
        return stored;
    }

    private async Task<string?> GetItemAsync(string key)
        => await _javaScript.InvokeAsync<string?>("rockBandSpotify.getItem", key);

    private async Task SetItemAsync(string key, string value)
        => await _javaScript.InvokeVoidAsync("rockBandSpotify.setItem", key, value);

    private async Task RemoveItemAsync(string key)
        => await _javaScript.InvokeVoidAsync("rockBandSpotify.removeItem", key);
}
