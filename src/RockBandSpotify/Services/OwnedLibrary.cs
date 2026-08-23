using System.Text.Json;
using Microsoft.JSInterop;
using RockBandSpotify.Models;

namespace RockBandSpotify.Services;

/// <summary>
/// Remembers which catalogue songs this browser's owner holds, so PlayStation
/// is asked once rather than on every visit.
///
/// <para>
/// Stored as <see cref="Models.CatalogueSong.Id"/> values rather than titles,
/// because resolving entitlements already establishes exactly which catalogue
/// rows an account owns; re-deriving that from titles later would only lose
/// information. Both the catalogue's ownership column and the Spotify matching
/// flow read this same list.
/// </para>
/// </summary>
public class OwnedLibrary
{
    private const string StorageKey = "rock_band_owned_songs";

    private readonly IJSRuntime _javaScript;

    public OwnedLibrary(IJSRuntime javaScript) => _javaScript = javaScript;

    /// <summary>The stored library, or null when nothing has been fetched.</summary>
    public async Task<SongLibrary?> LoadAsync()
    {
        try
        {
            var raw = await _javaScript.InvokeAsync<string?>("rockBandSpotify.getItem", StorageKey);
            if (string.IsNullOrEmpty(raw)) return null;
            return JsonSerializer.Deserialize<SongLibrary>(raw);
        }
        catch
        {
            // Unavailable storage, or a value written by an older build.
            return null;
        }
    }

    /// <summary>Just the owned ids — what the catalogue page needs to mark rows.</summary>
    public async Task<HashSet<int>> LoadIdsAsync()
    {
        var library = await LoadAsync();
        return library is null ? [] : [.. library.SongIds];
    }

    public async Task SaveAsync(SongLibrary library)
    {
        try
        {
            await _javaScript.InvokeVoidAsync("rockBandSpotify.setItem", StorageKey, JsonSerializer.Serialize(library));
        }
        catch { /* the list just won't survive a reload */ }
    }

    public async Task ClearAsync()
    {
        try
        {
            await _javaScript.InvokeVoidAsync("rockBandSpotify.removeItem", StorageKey);
        }
        catch { /* nothing to clear */ }
    }
}
