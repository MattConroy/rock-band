using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RockBandSpotify.Models;

namespace RockBandSpotify.Services;

/// <summary>Thin wrapper over the Spotify Web API endpoints this app needs.</summary>
public class SpotifyApiService : ITrackLookup
{
    private const string ApiBase = "https://api.spotify.com/v1";

    private readonly HttpClient _http;
    private readonly SpotifyAuthService _auth;

    public SpotifyApiService(HttpClient http, SpotifyAuthService auth)
    {
        _http = http;
        _auth = auth;
    }

    private async Task<HttpRequestMessage> AuthorizedRequest(HttpMethod method, string url)
    {
        var token = await _auth.GetAccessTokenAsync()
            ?? throw new InvalidOperationException("Not signed in to Spotify.");
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    /// <summary>Sends a request, transparently waiting out 429 rate limits once.</summary>
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, JsonContent? body = null)
    {
        if (body is not null)
            request.Content = body;

        var response = await _http.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2);
            await Task.Delay(retryAfter);
            var retry = await CloneAsync(request, body);
            response = await _http.SendAsync(retry);
        }
        return response;
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage original, JsonContent? body)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);
        foreach (var header in original.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        if (body is not null)
            clone.Content = body;
        return await Task.FromResult(clone);
    }

    public async Task<SpotifyUser> GetCurrentUserAsync()
    {
        var request = await AuthorizedRequest(HttpMethod.Get, $"{ApiBase}/me");
        var response = await SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SpotifyUser>())!;
    }

    /// <summary>Searches tracks by artist + title, returning up to <paramref name="limit"/> candidates.</summary>
    public async Task<List<SpotifyTrack>> SearchTracksAsync(string title, string artist, int limit = 5)
    {
        var q = $"track:{title} artist:{artist}";
        var url = $"{ApiBase}/search?type=track&limit={limit}&q={Uri.EscapeDataString(q)}";
        var request = await AuthorizedRequest(HttpMethod.Get, url);
        var response = await SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            // Fall back to a looser free-text query if the fielded search finds nothing useful.
            var loose = $"{ApiBase}/search?type=track&limit={limit}&q={Uri.EscapeDataString($"{title} {artist}")}";
            var looseReq = await AuthorizedRequest(HttpMethod.Get, loose);
            response = await SendAsync(looseReq);
            response.EnsureSuccessStatusCode();
        }

        var result = await response.Content.ReadFromJsonAsync<SpotifySearchResponse>();
        return result?.Tracks?.Items ?? new List<SpotifyTrack>();
    }

    /// <summary>
    /// Looks up tracks by id, fifty at a time — the endpoint's limit.
    /// <para>
    /// This is what makes a known id worth having: a library of 850 songs is
    /// seventeen requests here against 850 searches, and the answer needs no
    /// scoring because nothing was guessed. Ids Spotify no longer recognises
    /// come back as nulls and are simply absent from the result.
    /// </para>
    /// </summary>
    public async Task<Dictionary<string, SpotifyTrack>> GetTracksAsync(IReadOnlyList<string> ids)
    {
        var found = new Dictionary<string, SpotifyTrack>(StringComparer.OrdinalIgnoreCase);

        foreach (var batch in ids.Distinct(StringComparer.OrdinalIgnoreCase).Chunk(50))
        {
            var url = $"{ApiBase}/tracks?ids={Uri.EscapeDataString(string.Join(",", batch))}";
            var request = await AuthorizedRequest(HttpMethod.Get, url);
            var response = await SendAsync(request);
            if (!response.IsSuccessStatusCode) continue;

            var page = await response.Content.ReadFromJsonAsync<SpotifyTracksResponse>();
            foreach (var track in page?.Tracks ?? [])
                if (track is not null && !string.IsNullOrEmpty(track.Id))
                    found[track.Id] = track;
        }

        return found;
    }

    /// <summary>Finds a playlist owned by the user by exact (case-insensitive) name.</summary>
    public async Task<SpotifyPlaylist?> FindPlaylistByNameAsync(string name)
    {
        string? url = $"{ApiBase}/me/playlists?limit=50";
        while (url is not null)
        {
            var request = await AuthorizedRequest(HttpMethod.Get, url);
            var response = await SendAsync(request);
            response.EnsureSuccessStatusCode();
            var page = await response.Content.ReadFromJsonAsync<SpotifyPlaylistPage>();
            var match = page?.Items.FirstOrDefault(p =>
                string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match;
            url = page?.Next;
        }
        return null;
    }

    public async Task<SpotifyPlaylist> CreatePlaylistAsync(string userId, string name, string description, bool isPublic)
    {
        var request = await AuthorizedRequest(HttpMethod.Post, $"{ApiBase}/users/{userId}/playlists");
        var body = JsonContent.Create(new { name, description, @public = isPublic });
        var response = await SendAsync(request, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SpotifyPlaylist>())!;
    }

    /// <summary>Returns all track URIs currently in a playlist.</summary>
    public async Task<HashSet<string>> GetPlaylistTrackUrisAsync(string playlistId)
    {
        var uris = new HashSet<string>();
        string? url = $"{ApiBase}/playlists/{playlistId}/tracks?fields=items(track(uri)),next&limit=100";
        while (url is not null)
        {
            var request = await AuthorizedRequest(HttpMethod.Get, url);
            var response = await SendAsync(request);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            foreach (var item in root.GetProperty("items").EnumerateArray())
            {
                if (item.TryGetProperty("track", out var track) &&
                    track.ValueKind == JsonValueKind.Object &&
                    track.TryGetProperty("uri", out var uri) &&
                    uri.GetString() is { } u)
                {
                    uris.Add(u);
                }
            }
            url = root.TryGetProperty("next", out var next) && next.ValueKind == JsonValueKind.String
                ? next.GetString()
                : null;
        }
        return uris;
    }

    /// <summary>Adds track URIs to a playlist in batches of 100 (Spotify's max).</summary>
    public async Task AddTracksAsync(string playlistId, IReadOnlyList<string> uris)
    {
        foreach (var batch in Batch(uris, 100))
        {
            var request = await AuthorizedRequest(HttpMethod.Post, $"{ApiBase}/playlists/{playlistId}/tracks");
            var body = JsonContent.Create(new { uris = batch });
            var response = await SendAsync(request, body);
            response.EnsureSuccessStatusCode();
        }
    }

    private static IEnumerable<List<T>> Batch<T>(IReadOnlyList<T> items, int size)
    {
        for (var i = 0; i < items.Count; i += size)
            yield return items.Skip(i).Take(size).ToList();
    }
}
