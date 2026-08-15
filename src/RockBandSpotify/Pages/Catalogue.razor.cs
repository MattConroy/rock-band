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
    private List<string> _rb4Values = new();

    private string _search = "";
    private string _genre = "";
    private string _origin = "";
    private string _rb4 = "";
    private bool _selectedOnly;

    private bool HasFilters => _search.Length > 0 || _genre.Length > 0 || _origin.Length > 0
                                || _rb4.Length > 0 || _selectedOnly;

    protected override async Task OnInitializedAsync()
    {
        _all = (await Catalog.GetSongsAsync()).ToList();
        _genres = _all.Where(s => !string.IsNullOrEmpty(s.Genre)).Select(s => s.Genre!).Distinct().OrderBy(g => g).ToList();
        _origins = _all.Where(s => !string.IsNullOrEmpty(s.Origin)).Select(s => s.Origin!).Distinct().OrderBy(o => o).ToList();
        _rb4Values = _all.Where(s => !string.IsNullOrEmpty(s.Rb4)).Select(s => s.Rb4!).Distinct().OrderBy(r => r).ToList();
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

    private void OnRb4Changed(ChangeEventArgs e)
    {
        _rb4 = e.Value?.ToString() ?? "";
        ApplyFilters();
    }

    private void OnSelectedOnlyChanged(ChangeEventArgs e)
    {
        _selectedOnly = (bool)(e.Value ?? false);
        ApplyFilters();
    }

    private void ClearFilters()
    {
        _search = ""; _genre = ""; _origin = ""; _rb4 = ""; _selectedOnly = false;
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
    {
        IEnumerable<CatalogueSong> q = _all;

        if (_search.Length > 0)
            q = q.Where(s => s.Song.Contains(_search, StringComparison.OrdinalIgnoreCase)
                           || s.Artist.Contains(_search, StringComparison.OrdinalIgnoreCase));

        if (_genre.Length > 0)
            q = q.Where(s => s.Genre == _genre);

        if (_origin.Length > 0)
            q = q.Where(s => s.Origin == _origin);

        if (_rb4.Length > 0)
            q = q.Where(s => s.Rb4 == _rb4);

        if (_selectedOnly)
            q = q.Where(s => _selected.Contains(s.Id));

        _filtered = q.ToList();
    }
}
