using Microsoft.AspNetCore.Components;
using RockBandSpotify.Models;
using RockBandSpotify.Services;

namespace RockBandSpotify.Pages;

/// <summary>
/// Standalone browse/filter/select page over the static song catalogue.
/// Works with no login; selections live only in this page's memory for now.
/// </summary>
public partial class Catalogue
{
    [Inject] private CatalogueService Catalog { get; set; } = default!;

    private List<CatalogueSong> _all = new();
    private List<CatalogueSong> _filtered = new();
    private readonly HashSet<int> _selected = new();

    private List<string> _genres = new();
    private List<string> _origins = new();

    private string _search = "";
    private string _genre = "";
    private string _origin = "";
    private bool _selectedOnly;

    private bool HasFilters => _search.Length > 0 || _genre.Length > 0 || _origin.Length > 0
                                || _selectedOnly;

    protected override async Task OnInitializedAsync()
    {
        _all = (await Catalog.GetSongsAsync()).ToList();
        _genres = _all.Where(s => !string.IsNullOrEmpty(s.Genre)).Select(s => s.Genre!).Distinct().OrderBy(g => g).ToList();
        _origins = _all.Where(s => !string.IsNullOrEmpty(s.Origin)).Select(s => s.Origin!).Distinct().OrderBy(EraCatalog.Name).ToList();
        ApplyFilters();
    }

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

    private void OnSelectedOnlyChanged(ChangeEventArgs e)
    {
        _selectedOnly = (bool)(e.Value ?? false);
        ApplyFilters();
    }

    private void ClearFilters()
    {
        _search = ""; _genre = ""; _origin = ""; _selectedOnly = false;
        ApplyFilters();
    }

    private void ToggleSelected(int id, ChangeEventArgs e)
    {
        if ((bool)(e.Value ?? false))
            _selected.Add(id);
        else
            _selected.Remove(id);

        if (_selectedOnly)
            ApplyFilters();
    }

    private void ApplyFilters()
        => _filtered = CatalogueFilter.Apply(_all, _search, _genre, _origin, _selectedOnly, _selected);
}
