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
    [Inject] private OwnedLibrary Owned { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private const string ColumnsStorageKey = "rb_catalogue_columns";

    /// <summary>One optional (non-Song/Artist) column: its storage key, header
    /// label, and cell value.</summary>
    private sealed record ColumnDef(
        string Key,
        string Label,
        Func<CatalogueSong, string?> Value);

    private static readonly ColumnDef[] OptionalColumns =
    {
        new("Year", "Year", s => s.Year?.ToString()),
        new("Genre", "Genre", s => s.Genre),
        new("Source", "Source", s => SourceCatalog.Names(s.Sources)),
    };

    private List<CatalogueSong> _all = new();
    private List<CatalogueSong> _filtered = new();

    private List<string> _genres = new();
    private List<string> _sources = new();

    private string _search = "";
    private string _genre = "";
    private string _source = "";
    private OwnedFilter _owned = OwnedFilter.Any;

    // Empty until PlayStation has been connected and fetched at least once.
    private HashSet<int> _ownedIds = new();
    private bool HasOwnedLibrary => _ownedIds.Count > 0;

    private string? _sortColumn;
    private SortDirection _sortDirection = SortDirection.None;

    private readonly HashSet<string> _visibleColumns = new();
    private bool _showColumnPicker;

    private bool HasFilters => _search.Length > 0 || _genre.Length > 0 || _source.Length > 0
                               || _owned != OwnedFilter.Any;

    protected override void OnInitialized()
    {
        _visibleColumns.UnionWith(LoadColumnPreference() ?? DefaultColumnsForViewport());
    }

    protected override async Task OnInitializedAsync()
    {
        _all = (await Catalog.GetSongsAsync()).ToList();
        _ownedIds = await Owned.LoadAsync();
        _genres = _all.Where(s => !string.IsNullOrEmpty(s.Genre)).Select(s => s.Genre!).Distinct().OrderBy(g => g).ToList();
        // Every source a song lists, not just its origin, so the dropdown can
        // offer a game whose whole tracklist is songs that originated elsewhere.
        _sources = _all.SelectMany(s => s.Sources).Distinct().OrderBy(SourceCatalog.Name).ToList();
        ApplyFilters();
    }

    // Narrow: just Song/Artist. Everything else: + Year/Genre/Source.
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

        return width < 640 ? new HashSet<string>() : new HashSet<string> { "Year", "Genre", "Source" };
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

    private void OnSourceChanged(ChangeEventArgs e)
    {
        _source = e.Value?.ToString() ?? "";
        ApplyFilters();
    }

    private void OnOwnedChanged(ChangeEventArgs e)
    {
        _owned = Enum.TryParse<OwnedFilter>(e.Value?.ToString(), out var v) ? v : OwnedFilter.Any;
        ApplyFilters();
    }

    private void ClearFilters()
    {
        _search = ""; _genre = ""; _source = "";
        _owned = OwnedFilter.Any;
        ApplyFilters();
    }

    private void ClearSearch()
    {
        _search = "";
        ApplyFilters();
    }

    private void ApplyFilters()
        => _filtered = CatalogueSort.Apply(
            CatalogueFilter.Apply(_all, _search, _genre, _source, _owned, _ownedIds),
            _sortColumn, _sortDirection);

    // Tap cycle: unsorted -> ascending -> descending -> unsorted. Tapping a
    // different column starts it fresh at ascending.
    private void ToggleSort(string column)
    {
        if (_sortColumn != column)
        {
            _sortColumn = column;
            _sortDirection = SortDirection.Ascending;
        }
        else
        {
            _sortDirection = _sortDirection switch
            {
                SortDirection.None => SortDirection.Ascending,
                SortDirection.Ascending => SortDirection.Descending,
                _ => SortDirection.None,
            };
            if (_sortDirection == SortDirection.None) _sortColumn = null;
        }

        ApplyFilters();
    }

    private string SortIndicator(string column)
        => _sortColumn != column
            ? ""
            : _sortDirection switch { SortDirection.Ascending => "▲", SortDirection.Descending => "▼", _ => "" };

    private string? AriaSort(string column)
        => _sortColumn != column
            ? null
            : _sortDirection switch { SortDirection.Ascending => "ascending", SortDirection.Descending => "descending", _ => null };
}
