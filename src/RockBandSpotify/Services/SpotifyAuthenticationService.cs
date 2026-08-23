using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using RockBandSpotify.Models;

namespace RockBandSpotify.Services;

/// <summary>
/// Implements the Spotify Authorization Code flow with PKCE, which needs no
/// client secret and runs entirely in the browser — the right fit for a static
/// GitHub Pages SPA. Tokens are cached in localStorage and refreshed on demand.
/// </summary>
public class SpotifyAuthenticationService
{
    private const string AuthorizeEndpoint = "https://accounts.spotify.com/authorize";
    private const string TokenEndpoint = "https://accounts.spotify.com/api/token";
    private const string VerifierKey = "rock_band_pkce_verifier";
    private const string TokenKey = "rock_band_spotify_token";
    private const string ReturnPathKey = "rock_band_pkce_return_path";

    private readonly HttpClient _http;
    private readonly IJSRuntime _javaScript;
    private readonly NavigationManager _navigation;
    private readonly SpotifyConfig _configuration;
    private readonly string _redirectUri;

    public SpotifyAuthenticationService(HttpClient http, IJSRuntime javaScript, NavigationManager nav, SpotifyConfig configuration, string baseAddress)
    {
        _http = http;
        _javaScript = javaScript;
        _navigation = nav;
        _configuration = configuration;
        // Spotify requires an exact redirect URI match; this dedicated callback
        // route (e.g. https://user.github.io/rock-band/spotify-connect) must be
        // registered in the Spotify dashboard as a Redirect URI.
        _redirectUri = baseAddress + "spotify-connect";
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_configuration.ClientId)
                                && !_configuration.ClientId.StartsWith("REPLACE_");

    public string RedirectUri => _redirectUri;

    private StoredToken? _token;

    public async Task<bool> IsAuthenticatedAsync()
    {
        _token ??= await LoadTokenAsync();
        if (_token is null) return false;

        // A token granted before a scope was added still works for some calls
        // and 403s on others, which surfaces as a permission error nobody can
        // act on. Treat it as signed out so the next press asks Spotify again.
        if (!_token.Covers(_configuration.Scopes))
        {
            await LogoutAsync();
            return false;
        }

        return true;
    }

    /// <summary>Kicks off login by redirecting the whole page to Spotify.</summary>
    public async Task BeginLoginAsync()
    {
        var verifier = GenerateCodeVerifier();
        var challenge = GenerateCodeChallenge(verifier);
        await _javaScript.InvokeVoidAsync("rockBandSpotify.setItem", VerifierKey, verifier);
        // Remember where login was triggered from, so the callback page can
        // send the user back there instead of always landing on one fixed page.
        await _javaScript.InvokeVoidAsync("rockBandSpotify.setItem", ReturnPathKey, _navigation.ToBaseRelativePath(_navigation.Uri));

        var query = new Dictionary<string, string>
        {
            ["client_id"] = _configuration.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = _redirectUri,
            ["code_challenge_method"] = "S256",
            ["code_challenge"] = challenge,
            ["scope"] = _configuration.Scopes
        };
        var url = AuthorizeEndpoint + "?" + string.Join("&",
            query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        await _javaScript.InvokeVoidAsync("rockBandSpotify.redirect", url);
    }

    /// <summary>The page login was triggered from, so the callback page can
    /// return the user there. Clears the stashed value; defaults to home.</summary>
    public async Task<string> ConsumeReturnPathAsync()
    {
        var path = await _javaScript.InvokeAsync<string?>("rockBandSpotify.getItem", ReturnPathKey);
        await _javaScript.InvokeVoidAsync("rockBandSpotify.removeItem", ReturnPathKey);
        return string.IsNullOrEmpty(path) ? "/" : path;
    }

    /// <summary>
    /// Called on page load. If we returned from Spotify with ?code=..., exchange
    /// it for tokens and clean the URL. Returns true if a login completed.
    /// </summary>
    public async Task<bool> TryCompleteLoginAsync(string currentUrl)
    {
        var uri = new Uri(currentUrl);
        var queryParams = ParseQuery(uri.Query);
        if (!queryParams.TryGetValue("code", out var code))
            return false;

        var verifier = await _javaScript.InvokeAsync<string?>("rockBandSpotify.getItem", VerifierKey);
        if (string.IsNullOrEmpty(verifier))
            return false;

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _redirectUri,
            ["client_id"] = _configuration.ClientId,
            ["code_verifier"] = verifier
        };

        var response = await _http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form));
        // Always strip the spent code from the URL, success or not.
        await _javaScript.InvokeVoidAsync("rockBandSpotify.replaceUrl", _redirectUri);
        await _javaScript.InvokeVoidAsync("rockBandSpotify.removeItem", VerifierKey);

        if (!response.IsSuccessStatusCode)
            return false;

        var tokenResponse = await response.Content.ReadFromJsonAsync<SpotifyTokenResponse>();
        if (tokenResponse is null)
            return false;

        await StoreTokenAsync(tokenResponse);
        return true;
    }

    /// <summary>Returns a valid access token, refreshing if needed. Null if signed out.</summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        _token ??= await LoadTokenAsync();
        if (_token is null)
            return null;

        if (_token.IsExpired)
        {
            if (!await RefreshAsync())
                return null;
        }
        return _token?.AccessToken;
    }

    public async Task LogoutAsync()
    {
        _token = null;
        await _javaScript.InvokeVoidAsync("rockBandSpotify.removeItem", TokenKey);
    }

    private async Task<bool> RefreshAsync()
    {
        if (_token?.RefreshToken is null)
        {
            await LogoutAsync();
            return false;
        }

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = _token.RefreshToken,
            ["client_id"] = _configuration.ClientId
        };
        var response = await _http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form));
        if (!response.IsSuccessStatusCode)
        {
            await LogoutAsync();
            return false;
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<SpotifyTokenResponse>();
        if (tokenResponse is null)
        {
            await LogoutAsync();
            return false;
        }

        // Spotify may omit a new refresh token; keep the existing one if so.
        tokenResponse.RefreshToken ??= _token.RefreshToken;
        await StoreTokenAsync(tokenResponse);
        return true;
    }

    private async Task StoreTokenAsync(SpotifyTokenResponse response)
    {
        _token = new StoredToken
        {
            AccessToken = response.AccessToken,
            RefreshToken = response.RefreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn),
            // What Spotify actually granted, which can be narrower than asked.
            Scope = response.Scope ?? _configuration.Scopes,
        };
        await _javaScript.InvokeVoidAsync("rockBandSpotify.setItem", TokenKey, JsonSerializer.Serialize(_token));
    }

    private async Task<StoredToken?> LoadTokenAsync()
    {
        var raw = await _javaScript.InvokeAsync<string?>("rockBandSpotify.getItem", TokenKey);
        if (string.IsNullOrEmpty(raw))
            return null;
        try
        {
            return JsonSerializer.Deserialize<StoredToken>(raw);
        }
        catch
        {
            return null;
        }
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateCodeChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>();
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2)
                result[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]);
        }
        return result;
    }
}
