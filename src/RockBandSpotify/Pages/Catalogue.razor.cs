using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using RockBandSpotify.Models;
using RockBandSpotify.Services;

namespace RockBandSpotify.Pages;

/// <summary>
/// Standalone browse/filter page over the static song catalogue.
/// Works with no login. Column visibility defaults to the viewport width on
/// first load, then follows whatever the user picks in the column customizer
/// (persisted to localStorage).
/// </summary>
public partial class Catalogue
{
    [Inject] private CatalogueService Catalog { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private const string ColumnsStorageKey = "rb_catalogue_columns";

    /// <summary>One optional (non-Song/Artist) column: its storage key, header
    /// label, cell value, and an optional tooltip for the cell.</summary>
    private sealed record ColumnDef(
        string Key,
        string Label,
        Func<CatalogueSong, string?> Value,
        Func<CatalogueSong, string?>? Title = null);

    private static readonly ColumnDef[] OptionalColumns =
    {
        new("Year", "Year", s => s.Year?.ToString()),
        new("Genre", "Genre", s => s.Genre),
        new("Era", "Era", s => EraCatalog.Name(s.Origin), s => EraCatalog.Description(s.Origin)),
    };

    private List<CatalogueSong> _all = new();
    private List<CatalogueSong> _filtered = new();

    private List<string> _genres = new();
    private List<string> _origins = new();

    private string _search = "";
    private string _genre = "";
    private string _origin = "";

    private readonly HashSet<string> _visibleColumns = new();
    private bool _showColumnPicker;

    private bool HasFilters => _search.Length > 0 || _genre.Length > 0 || _origin.Length > 0;

    // Spotify's OAuth redirect always returns to the app's base address, which
    // is this page now that the catalogue is the homepage. If we're completing
    // a login, hand off to the connect page (preserving the query string)
    // instead of loading the catalogue.
    private bool _redirectingToConnect;

    protected override void OnInitialized()
    {
        var query = new Uri(Nav.Uri).Query;
        if (query.Contains("code=") && query.Contains("state="))
        {
            _redirectingToConnect = true;
            Nav.NavigateTo("/connect" + query);
            return;
        }

        _visibleColumns.UnionWith(LoadColumnPreference() ?? DefaultColumnsForViewport());
    }

    protected override async Task OnInitializedAsync()
    {
        if (_redirectingToConnect) return;

        _all = (await Catalog.GetSongsAsync()).ToList();
        _genres = _all.Where(s => !string.IsNullOrEmpty(s.Genre)).Select(s => s.Genre!).Distinct().OrderBy(g => g).ToList();
        _origins = _all.Where(s => !string.IsNullOrEmpty(s.Origin)).Select(s => s.Origin!).Distinct().OrderBy(EraCatalog.Name).ToList();
        ApplyFilters();
    }

    // Narrow: just Song/Artist. Everything else: + Year/Genre/Era.
    private HashSet<string> DefaultColumnsForViewport()
    {
        int width;
        try
        {
            width = ((IJSInProcessRuntime)JS).Invoke<int>("rbSpotify.getViewportWidth");
        }
        catch
        {
            width = 1024; // pre-rendering or non-WASM host: assume desktop
        }

        return width < 640 ? new HashSet<string>() : new HashSet<string> { "Year", "Genre", "Era" };
    }

    private HashSet<string>? LoadColumnPreference()
    {
        try
        {
            var raw = ((IJSInProcessRuntime)JS).Invoke<string?>("rbSpotify.getItem", ColumnsStorageKey);
            if (string.IsNullOrEmpty(raw)) return null;
            var keys = JsonSerializer.Deserialize<string[]>(raw);
            return keys is null ? null : new HashSet<string>(keys);
        }
        catch
        {
            return null;
        }
    }

    private void SaveColumnPreference()
    {
        try
        {
            ((IJSInProcessRuntime)JS).InvokeVoid("rbSpotify.setItem", ColumnsStorageKey, JsonSerializer.Serialize(_visibleColumns));
        }
        catch { /* localStorage unavailable — preference just won't persist */ }
    }

    private void ToggleColumn(string key)
    {
        if (!_visibleColumns.Remove(key))
            _visibleColumns.Add(key);
        SaveColumnPreference();
    }

    private void ToggleColumnPicker() => _showColumnPicker = !_showColumnPicker;

    private void OnSearchChanged(ChangeEventArgs e)
    {
        _search = e.Value?.ToString() ?? "";
        ApplyFilters();
    }

    private void OnGenreChanged(ChangeEventArgs e)
    {
        _genre = e.Value?.ToString() ?? "";
        ApplyFilters();
    }

    private void OnOriginChanged(ChangeEventArgs e)
    {
        _origin = e.Value?.ToString() ?? "";
        ApplyFilters();
    }

    private void ClearFilters()
    {
        _search = ""; _genre = ""; _origin = "";
        ApplyFilters();
    }

    private void ClearSearch()
    {
        _search = "";
        ApplyFilters();
    }

    private void ApplyFilters()
        => _filtered = CatalogueFilter.Apply(_all, _search, _genre, _origin);
}
