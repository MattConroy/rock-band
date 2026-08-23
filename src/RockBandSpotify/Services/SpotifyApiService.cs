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
    private readonly SpotifyAuthenticationService _authentication;

    public SpotifyApiService(HttpClient http, SpotifyAuthenticationService authentication)
    {
        _http = http;
        _authentication = authentication;
    }

    private async Task<HttpRequestMessage> AuthorizedRequest(HttpMethod method, string url)
    {
        var token = await _authentication.GetAccessTokenAsync()
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

    /// <summary>
    /// Fails with something a person can act on. EnsureSuccessStatusCode throws
    /// "net_http_message_not_success_statuscode_reason, 403, Forbidden" — no
    /// endpoint, no reason — which says nothing about whether the sign-in is
    /// short a scope, the playlist belongs to someone else, or Spotify is
    /// simply down.
    /// </summary>
    private static async Task EnsureAsync(HttpResponseMessage response, string what)
    {
        if (response.IsSuccessStatusCode) return;

        var detail = await ReadErrorMessageAsync(response);
        var message = $"Spotify refused {what} ({(int)response.StatusCode} {response.ReasonPhrase})"
            + (detail is null ? "." : $": {detail}");

        // A 403 after a successful sign-in almost always means the token was
        // granted before the app asked for the scope this call needs.
        if (response.StatusCode == HttpStatusCode.Forbidden)
            message += " Disconnect Spotify and sign in again — the app may need a permission your sign-in didn't cover.";

        throw new InvalidOperationException(message);
    }

    /// <summary>Spotify puts a human-readable reason in the body; dig it out.</summary>
    private static async Task<string?> ReadErrorMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(body)) return null;
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String) return error.GetString();
                if (error.TryGetProperty("message", out var m)) return m.GetString();
            }
            return null;
        }
        catch
        {
            return null;
        }
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

    /// <summary>Searches tracks by artist + title, returning up to <paramref name="limit"/> candidates.</summary>
    public async Task<List<SpotifyTrack>> SearchTracksAsync(string title, string artist, int limit = 5)
    {
        var q = $"track:{title} artist:{artist}";
        var url = $"{ApiBase}/search?type=track&limit={Clamp(limit)}&q={Uri.EscapeDataString(q)}";
        var request = await AuthorizedRequest(HttpMethod.Get, url);
        var response = await SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            // Fall back to a looser free-text query if the fielded search finds nothing useful.
            var loose = $"{ApiBase}/search?type=track&limit={Clamp(limit)}&q={Uri.EscapeDataString($"{title} {artist}")}";
            var looseReq = await AuthorizedRequest(HttpMethod.Get, loose);
            response = await SendAsync(looseReq);
            await EnsureAsync(response, "the track search");
        }

        var result = await response.Content.ReadFromJsonAsync<SpotifySearchResponse>();
        return result?.Tracks?.Items ?? new List<SpotifyTrack>();
    }

    /// <summary>
    /// Spotify cut search's maximum limit from 50 to 10 in February 2026, and
    /// asking for more is a 400. Clamped rather than trusted, because the
    /// caller that eventually wants more candidates shouldn't have to know.
    /// </summary>
    internal const int MaxSearchLimit = 10;

    private static int Clamp(int limit) => Math.Clamp(limit, 1, MaxSearchLimit);

    /// <summary>Finds a playlist owned by the user by exact (case-insensitive) name.</summary>
    public async Task<SpotifyPlaylist?> FindPlaylistByNameAsync(string name)
    {
        string? url = $"{ApiBase}/me/playlists?limit=50";
        while (url is not null)
        {
            var request = await AuthorizedRequest(HttpMethod.Get, url);
            var response = await SendAsync(request);
            await EnsureAsync(response, "the playlist list (GET /me/playlists)");
            var page = await response.Content.ReadFromJsonAsync<SpotifyPlaylistPage>();
            var match = page?.Items.FirstOrDefault(p =>
                string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match;
            url = page?.Next;
        }
        return null;
    }

    /// <summary>
    /// Creates the playlist on the signed-in account. Spotify retired
    /// POST /users/{id}/playlists — it now answers 403 for every caller — and
    /// names POST /me/playlists as the replacement, which needs no user id.
    /// </summary>
    public async Task<SpotifyPlaylist> CreatePlaylistAsync(string name, string description, bool isPublic)
    {
        var request = await AuthorizedRequest(HttpMethod.Post, $"{ApiBase}/me/playlists");
        var body = JsonContent.Create(new { name, description, @public = isPublic });
        var response = await SendAsync(request, body);
        await EnsureAsync(response, "creating the playlist");
        return (await response.Content.ReadFromJsonAsync<SpotifyPlaylist>())!;
    }

    /// <summary>Returns all track URIs currently in a playlist.</summary>
    public async Task<HashSet<string>> GetPlaylistTrackUrisAsync(string playlistId)
    {
        var uris = new HashSet<string>();
        string? url = $"{ApiBase}/playlists/{playlistId}/items?fields=items(track(uri)),next&limit=100";
        while (url is not null)
        {
            var request = await AuthorizedRequest(HttpMethod.Get, url);
            var response = await SendAsync(request);
            await EnsureAsync(response, "reading the playlist's items");
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
            var request = await AuthorizedRequest(HttpMethod.Post, $"{ApiBase}/playlists/{playlistId}/items");
            var body = JsonContent.Create(new { uris = batch });
            var response = await SendAsync(request, body);
            await EnsureAsync(response, "adding items to the playlist");
        }
    }

    private static IEnumerable<List<T>> Batch<T>(IReadOnlyList<T> items, int size)
    {
        for (var i = 0; i < items.Count; i += size)
            yield return items.Skip(i).Take(size).ToList();
    }
}
