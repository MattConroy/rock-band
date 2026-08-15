using Microsoft.Playwright;

namespace RockBandSpotify.EndToEndTests;

/// <summary>
/// Browser tests for the standalone /catalogue page — the parts that only a
/// real rendered page can prove: filtering, responsive column defaults, the
/// column customizer, and layout.
/// </summary>
public class CatalogueTests : AppPageTest
{
    [SetUp]
    public async Task GotoCatalogue()
    {
        await Page.GotoAsync("/catalogue");
        // The heading renders immediately, but the catalogue itself loads
        // asynchronously (fetch + WASM deserialize) — wait for the row count
        // so tests don't race the data load.
        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Loads_the_full_catalogue()
    {
        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Root_path_also_shows_the_catalogue()
    {
        await Page.GotoAsync("/");
        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Standard_viewport_defaults_to_song_artist_year_genre_source()
    {
        // Default Playwright viewport (1280x720) is well above the narrow
        // breakpoint, so all three optional columns show.
        var headers = await Page.Locator("th").AllInnerTextsAsync();
        Assert.That(headers, Is.EqualTo(new[] { "SONG", "ARTIST", "YEAR", "GENRE", "SOURCE" }));
    }

    [Test]
    public async Task Narrow_viewport_defaults_to_song_and_artist_only()
    {
        await Page.SetViewportSizeAsync(400, 800);
        await Page.ReloadAsync();
        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync();

        var headers = await Page.Locator("th").AllInnerTextsAsync();
        Assert.That(headers, Is.EqualTo(new[] { "SONG", "ARTIST" }));
    }

    [Test]
    public async Task Column_picker_toggles_a_column_on_and_off()
    {
        await Page.GetByLabel("Choose columns").ClickAsync();
        var yearCheckbox = Page.GetByRole(AriaRole.Checkbox, new() { Name = "Year" });

        await Expect(yearCheckbox).ToBeCheckedAsync(); // on by default at this width
        await yearCheckbox.UncheckAsync();

        var headers = await Page.Locator("th").AllInnerTextsAsync();
        Assert.That(headers, Does.Not.Contain("YEAR"));

        await yearCheckbox.CheckAsync();
        headers = await Page.Locator("th").AllInnerTextsAsync();
        Assert.That(headers, Does.Contain("YEAR"));
    }

    [Test]
    public async Task Column_choice_survives_a_reload()
    {
        await Page.GetByLabel("Choose columns").ClickAsync();
        await Page.GetByRole(AriaRole.Checkbox, new() { Name = "Year" }).UncheckAsync();

        await Page.ReloadAsync();
        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync();

        var headers = await Page.Locator("th").AllInnerTextsAsync();
        Assert.That(headers, Does.Not.Contain("YEAR"));
    }

    [Test]
    public async Task Source_dropdown_shows_full_names_not_raw_codes()
    {
        var options = await Page.Locator("select").Nth(1).Locator("option").AllInnerTextsAsync();
        Assert.That(options, Does.Contain("Rock Band 1"));
        Assert.That(options, Does.Contain("Rock Band Network 2"));
        Assert.That(options, Does.Not.Contain("RB1"));
        Assert.That(options, Does.Not.Contain("RBN2"));
    }

    [Test]
    public async Task Source_cell_has_a_tooltip_explaining_the_code()
    {
        // Column order at the default wide viewport: Song, Artist, Year, Genre, Source, ...
        var cell = Page.Locator("tbody td").Nth(4);
        var title = await cell.GetAttributeAsync("title");
        Assert.That(title, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task Search_narrows_to_the_matching_song()
    {
        await Page.GetByPlaceholder("Search song or artist…").FillAsync("believer");
        await Expect(Page.GetByText("1 shown")).ToBeVisibleAsync();
        await Expect(Page.Locator("tbody")).ToContainTextAsync("Believer");
        await Expect(Page.Locator("tbody")).ToContainTextAsync("Imagine Dragons");
    }

    [Test]
    public async Task Search_can_return_several_songs_with_the_same_title()
    {
        await Page.GetByPlaceholder("Search song or artist…").FillAsync("africa");
        var rows = Page.Locator("tbody tr");
        await Expect(rows).ToHaveCountAsync(3); // Toto, Weezer, and the delisted RBN1 cover
    }

    [Test]
    public async Task Clear_button_on_the_search_field_resets_just_the_search()
    {
        var search = Page.GetByPlaceholder("Search song or artist…");
        var clearSearch = Page.GetByTitle("Clear search");

        await Expect(clearSearch).Not.ToBeVisibleAsync();

        await search.FillAsync("believer");
        await Expect(Page.GetByText("1 shown")).ToBeVisibleAsync();
        await Expect(clearSearch).ToBeVisibleAsync();

        await clearSearch.ClickAsync();
        await Expect(search).ToHaveValueAsync("");
        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Only_the_grid_scrolls_not_the_page()
    {
        var pageScrollable = await Page.EvaluateAsync<bool>(
            "document.documentElement.scrollHeight > document.documentElement.clientHeight + 1");
        Assert.That(pageScrollable, Is.False, "the outer page should never scroll on the catalogue view");

        var wrapScrollable = await Page.EvalOnSelectorAsync<bool>(
            ".table-wrap", "el => el.scrollHeight > el.clientHeight");
        Assert.That(wrapScrollable, Is.True, "the grid itself should be the scrolling region");
    }

    [Test]
    public async Task No_console_errors_while_browsing()
    {
        var errors = new List<string>();
        Page.Console += (_, msg) =>
        {
            if (msg.Type == "error") errors.Add(msg.Text);
        };

        await Page.GetByPlaceholder("Search song or artist…").FillAsync("metallica");
        await Page.Locator("select").First.SelectOptionAsync(new SelectOptionValue { Label = "Metal" });
        await Page.GetByText("Clear filters").ClickAsync();

        Assert.That(errors, Is.Empty);
    }
}
