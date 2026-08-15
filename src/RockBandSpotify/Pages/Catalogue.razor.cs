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

    // code -> (full name, one-line explanation), for the Era filter and column.
    // Covers every Origin value in the catalogue; unrecognized codes fall back to
    // showing the raw code with no tooltip.
    private static readonly Dictionary<string, (string Name, string Description)> EraInfo = new()
    {
        ["RB1"] = ("Rock Band 1", "On-disc tracklist of the original Rock Band (2007)."),
        ["RB2"] = ("Rock Band 2", "On-disc tracklist of Rock Band 2 (2008)."),
        ["RB3"] = ("Rock Band 3", "On-disc tracklist of Rock Band 3 (2010)."),
        ["RB4"] = ("Rock Band 4", "On-disc tracklist of Rock Band 4 (2015)."),
        ["DLC1/2"] = ("Rock Band 1 & 2 DLC", "Downloadable songs released during the Rock Band 1/2 era (2007–2009)."),
        ["DLC3"] = ("Rock Band 3 DLC", "Downloadable songs released during the Rock Band 3 era (2010–2013)."),
        ["DLC4"] = ("Rock Band 4 DLC", "Downloadable songs released for Rock Band 4 (2015 onward)."),
        ["RBN1"] = ("Rock Band Network 1", "Community-authored songs, officially licensed and sold (2010–2013)."),
        ["RBN2"] = ("Rock Band Network 2", "Community-authored songs from the relaunched Network (2015–2016)."),
        ["LEGO"] = ("LEGO Rock Band", "On-disc tracklist of the LEGO-themed spin-off (2009)."),
        ["GDRB"] = ("Green Day: Rock Band", "On-disc tracklist of the Green Day-themed spin-off (2010)."),
        ["TBRB"] = ("The Beatles: Rock Band", "On-disc tracklist of the Beatles-themed spin-off (2009)."),
        ["TBRB DLC"] = ("The Beatles: Rock Band DLC", "Downloadable songs for the Beatles-themed spin-off."),
        ["RBVR"] = ("Rock Band VR", "Tracklist exclusive to the VR spin-off (2016)."),
        ["CTP2"] = ("Country Track Pack 2", "A themed multi-song pack of country tracks."),
        ["AC/DC TP"] = ("AC/DC Track Pack", "A themed multi-song pack of AC/DC tracks."),
    };

    private static string EraName(string? code) =>
        code is not null && EraInfo.TryGetValue(code, out var info) ? info.Name : code ?? "";

    private static string? EraDescription(string? code) =>
        code is not null && EraInfo.TryGetValue(code, out var info) ? info.Description : null;

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
        _origins = _all.Where(s => !string.IsNullOrEmpty(s.Origin)).Select(s => s.Origin!).Distinct().OrderBy(EraName).ToList();
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
    {
        IEnumerable<CatalogueSong> q = _all;

        if (_search.Length > 0)
            q = q.Where(s => s.Song.Contains(_search, StringComparison.OrdinalIgnoreCase)
                           || s.Artist.Contains(_search, StringComparison.OrdinalIgnoreCase));

        if (_genre.Length > 0)
            q = q.Where(s => s.Genre == _genre);

        if (_origin.Length > 0)
            q = q.Where(s => s.Origin == _origin);

        if (_selectedOnly)
            q = q.Where(s => _selected.Contains(s.Id));

        _filtered = q.ToList();
    }
}
