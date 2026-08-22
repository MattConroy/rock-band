using System.Text.Json;
using Microsoft.JSInterop;

namespace RockBandSpotify.Services;

/// <summary>
/// Which catalogue songs this browser's owner holds, kept in localStorage as a
/// set of <see cref="Models.CatalogueSong.Id"/>.
///
/// <para>
/// Stored as ids rather than titles because the PSN resolve step already knows
/// exactly which catalogue rows an account's entitlements name — re-deriving
/// that from titles later would only lose information. It also means the
/// catalogue page can mark and filter without talking to PlayStation at all:
/// the fetch happens once, and every later visit reads the set back.
/// </para>
/// </summary>
public class OwnedLibrary
{
    private const string StorageKey = "rb_owned_song_ids";

    private readonly IJSRuntime _js;

    public OwnedLibrary(IJSRuntime js) => _js = js;

    /// <summary>The owned song ids, or an empty set when nothing has been fetched.</summary>
    public async Task<HashSet<int>> LoadAsync()
    {
        try
        {
            var raw = await _js.InvokeAsync<string?>("rbSpotify.getItem", StorageKey);
            if (string.IsNullOrEmpty(raw)) return [];
            return JsonSerializer.Deserialize<HashSet<int>>(raw) ?? [];
        }
        catch
        {
            // localStorage unavailable, or a value written by an older build.
            return [];
        }
    }

    public async Task SaveAsync(IEnumerable<int> songIds)
    {
        try
        {
            await _js.InvokeVoidAsync("rbSpotify.setItem", StorageKey, JsonSerializer.Serialize(songIds));
        }
        catch { /* the list just won't survive a reload */ }
    }

    public async Task ClearAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("rbSpotify.removeItem", StorageKey);
        }
        catch { /* nothing to clear */ }
    }
}
