using System.Net.Http.Json;
using RockBandSpotify.Models;

namespace RockBandSpotify.Services;

/// <summary>Loads the owned-song list produced by the PSN refresh workflow.</summary>
public class SongLibraryService
{
    private readonly HttpClient _http;

    public SongLibraryService(HttpClient http)
    {
        _http = http;
    }

    public async Task<SongLibrary> LoadAsync()
    {
        // Cache-bust so a freshly-committed songs.json is picked up promptly.
        var url = $"data/songs.json?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var library = await _http.GetFromJsonAsync<SongLibrary>(url)
                      ?? new SongLibrary();

        // De-duplicate by artist+title, preserving order.
        var seen = new HashSet<string>();
        library.Songs = library.Songs
            .Where(s => !string.IsNullOrWhiteSpace(s.Title) && seen.Add(s.Key))
            .OrderBy(s => s.Artist)
            .ThenBy(s => s.Title)
            .ToList();

        return library;
    }
}
