using System.Net.Http.Json;
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
        _songs ??= await _http.GetFromJsonAsync<List<CatalogueSong>>("data/catalogue.json")
                   ?? new List<CatalogueSong>();
        return _songs;
    }
}
