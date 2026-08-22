using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using RockBandSpotify.Models;

namespace RockBandSpotify.Services;

/// <summary>
/// Loads the static Rock Band song catalogue (wwwroot/data/catalogue.json) once
/// and caches it in memory. No PSN or Spotify login required to browse it.
/// </summary>
public class CatalogueService
{
    private readonly HttpClient _http;
    private List<CatalogueSong>? _songs;

    public CatalogueService(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<CatalogueSong>> GetSongsAsync()
    {
        if (_songs is not null) return _songs;

        // GitHub Pages serves everything with max-age=600 and offers no way to
        // change that, so a plain fetch can hand back a catalogue up to ten
        // minutes older than the app asking for it — and the file changes on
        // almost every release. Asking the browser to revalidate turns that
        // into a conditional request: a 304 and no body when nothing moved,
        // the new file the moment it does.
        var request = new HttpRequestMessage(HttpMethod.Get, "data/catalogue.json");
        request.SetBrowserRequestCache(BrowserRequestCache.NoCache);

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        _songs = await response.Content.ReadFromJsonAsync<List<CatalogueSong>>() ?? [];
        return _songs;
    }
}
