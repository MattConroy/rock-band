using System.Globalization;
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
    /// <summary>
    /// Set by the header's PlayStation button once a library has been fetched.
    /// Carried in the address so the view survives a reload and can be shared.
    /// </summary>
    [SupplyParameterFromQuery(Name = "owned")]
    private string? OwnedQuery { get; set; }

    [Inject] private CatalogueService Catalog { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private OwnedLibrary Owned { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private const string ColumnsStorageKey = "rock_band_catalogue_columns";

    /// <summary>One optional (non-Song/Artist) column: its storage key, header
    /// label, and cell value.</summary>
    /// <param name="Rem">The column's width. Every column declares one and
    /// none is left to take "the rest": under table-layout: fixed an unsized
    /// column absorbs the whole shortfall, which is how Song ended up 72px
    /// wide beside a 187px Artist. Surplus on a wide screen is shared out
    /// proportionally instead, which keeps the relative sizes.</param>
    private sealed record ColumnDef(
        string Key,
        string Label,
        Func<CatalogueSong, string?> Value,
        double Rem);

    // Widths live here rather than in the stylesheet so that the table's
    // minimum width is always the sum of exactly what is on screen. Split
    // across two files they drift, and the arithmetic silently stops
    // describing the layout.
    private const double OwnedRem = 2;
    private const double SongRem = 11;
    private const double ArtistRem = 8;

    private static string Width(double rem) =>
        $"width:{rem.ToString(CultureInfo.InvariantCulture)}rem";

    private static readonly ColumnDef[] OptionalColumns =
    {
        new("Year", "Year", s => s.Year?.ToString(), 3.5),
        new("Genre", "Genre", s => s.Genre, 6.5),
        new("Source", "Source", s => SourceCatalog.Names(s.Sources), 11),
        // When it came to Rock Band, as opposed to Year, which is when the
        // song came out. Invariant so the rendering doesn't shift with the
        // browser's locale.
        new("Released", "Released", s => s.ReleaseDate?.ToString("d MMM yyyy", CultureInfo.InvariantCulture), 6.5),
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

    /// <summary>
    /// The grid never shrinks below the sum of its visible columns; a phone
    /// showing every one of them can't fit them all, and the honest answer is
    /// to scroll sideways rather than crush Source to a character per line.
    /// The default three still fit a 390px screen with room to spare.
    /// </summary>
    private string TableMinWidth
    {
        get
        {
            var rem = SongRem + ArtistRem;
            if (HasOwnedLibrary) rem += OwnedRem;
            foreach (var col in OptionalColumns)
                if (_visibleColumns.Contains(col.Key)) rem += col.Rem;
            return $"min-width:{rem.ToString(CultureInfo.InvariantCulture)}rem";
        }
    }

    private bool HasFilters => _search.Length > 0 || _genre.Length > 0 || _source.Length > 0
                               || _owned != OwnedFilter.Any;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        try
        {
            await JS.InvokeVoidAsync("rockBandSpotify.syncTableScroll", "rock-band-table-header", "rock-band-table-body");
        }
        catch { /* pre-rendering or non-WASM host: the header just won't follow */ }
    }

    protected override void OnInitialized()
    {
        _visibleColumns.UnionWith(LoadColumnPreference() ?? DefaultColumnsForViewport());
    }

    protected override async Task OnParametersSetAsync()
    {
        var wanted = FilterFor(OwnedQuery);
        if (_all.Count > 0 && wanted != _owned)
        {
            _owned = wanted;
            ApplyFilters();
        }
    }

    /// <summary>
    /// The filter has three states, so the address needs three too. Leaving
    /// "unowned" out of it turned that choice back into "all songs" the moment
    /// anything re-read the address.
    /// </summary>
    private static string? QueryFor(OwnedFilter filter) => filter switch
    {
        OwnedFilter.Owned => "1",
        OwnedFilter.NotOwned => "0",
        _ => null,
    };

    private static OwnedFilter FilterFor(string? query) => query switch
    {
        "1" => OwnedFilter.Owned,
        "0" => OwnedFilter.NotOwned,
        _ => OwnedFilter.Any,
    };

    protected override async Task OnInitializedAsync()
    {
        _all = (await Catalog.GetSongsAsync()).ToList();
        _ownedIds = await Owned.LoadIdsAsync();
        _owned = FilterFor(OwnedQuery);
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
            width = ((IJSInProcessRuntime)JS).Invoke<int>("rockBandSpotify.getViewportWidth");
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
            var raw = ((IJSInProcessRuntime)JS).Invoke<string?>("rockBandSpotify.getItem", ColumnsStorageKey);
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
            ((IJSInProcessRuntime)JS).InvokeVoid("rockBandSpotify.setItem", ColumnsStorageKey, JsonSerializer.Serialize(_visibleColumns));
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
        => SetOwned(Enum.TryParse<OwnedFilter>(e.Value?.ToString(), out var v) ? v : OwnedFilter.Any);

    private void ClearFilters()
    {
        _search = ""; _genre = ""; _source = "";
        SetOwned(OwnedFilter.Any);
    }

    /// <summary>
    /// Changes the owned filter through the address rather than the field, so
    /// the header button and this page can't disagree about whether the
    /// catalogue is narrowed. Clearing the filters here used to leave owned=1
    /// in the address, which left the button offering to undo a filter that
    /// was already gone — and its opposite unreachable.
    /// </summary>
    private void SetOwned(OwnedFilter wanted)
    {
        var uri = Navigation.GetUriWithQueryParameter("owned", QueryFor(wanted));
        if (uri == Navigation.Uri)
        {
            // Already at that address, so no navigation will arrive to apply it.
            _owned = wanted;
            ApplyFilters();
            return;
        }

        Navigation.NavigateTo(uri);
    }

    private void ClearSearch()
    {
        _search = "";
        ApplyFilters();
    }

    private void ApplyFilters()
        => _filtered = CatalogueSort.Apply(
            CatalogueFilter.Apply(_all, _search, _genre, _source, _owned, _ownedIds),
            _sortColumn, _sortDirection, _ownedIds);

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
