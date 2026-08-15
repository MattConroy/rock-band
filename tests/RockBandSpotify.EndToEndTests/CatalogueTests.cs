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
        await Expect(Page.GetByText("4960 shown")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Loads_the_full_catalogue()
    {
        await Expect(Page.GetByText("4960 shown")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Wide_viewport_shows_song_artist_and_the_wide_default_columns()
    {
        // Default Playwright viewport (1280x720) is "wide" — Year/Genre/Era plus
        // RB1/2, RB3, Other. RB4 stays customizer-only at every width.
        var headers = await Page.Locator("th").AllInnerTextsAsync();
        Assert.That(headers, Is.EqualTo(new[] { "SONG", "ARTIST", "YEAR", "GENRE", "ERA", "RB1/2", "RB3", "OTHER" }));
    }

    [Test]
    public async Task Narrow_viewport_defaults_to_song_and_artist_only()
    {
        await Page.SetViewportSizeAsync(400, 800);
        await Page.ReloadAsync();
        await Expect(Page.GetByText("4960 shown")).ToBeVisibleAsync();

        var headers = await Page.Locator("th").AllInnerTextsAsync();
        Assert.That(headers, Is.EqualTo(new[] { "SONG", "ARTIST" }));
    }

    [Test]
    public async Task Column_picker_toggles_a_column_on_and_off()
    {
        await Page.GetByText("Columns ▾").ClickAsync();
        var rb4Checkbox = Page.GetByRole(AriaRole.Checkbox, new() { Name = "RB4" });

        await Expect(rb4Checkbox).Not.ToBeCheckedAsync(); // off by default at every width
        await rb4Checkbox.CheckAsync();

        var headers = await Page.Locator("th").AllInnerTextsAsync();
        Assert.That(headers, Does.Contain("RB4"));

        await rb4Checkbox.UncheckAsync();
        headers = await Page.Locator("th").AllInnerTextsAsync();
        Assert.That(headers, Does.Not.Contain("RB4"));
    }

    [Test]
    public async Task Column_choice_survives_a_reload()
    {
        await Page.GetByText("Columns ▾").ClickAsync();
        await Page.GetByRole(AriaRole.Checkbox, new() { Name = "RB4" }).CheckAsync();

        await Page.ReloadAsync();
        await Expect(Page.GetByText("4960 shown")).ToBeVisibleAsync();

        var headers = await Page.Locator("th").AllInnerTextsAsync();
        Assert.That(headers, Does.Contain("RB4"));
    }

    [Test]
    public async Task Era_dropdown_shows_full_names_not_raw_codes()
    {
        var options = await Page.Locator("select").Nth(1).Locator("option").AllInnerTextsAsync();
        Assert.That(options, Does.Contain("Rock Band 1"));
        Assert.That(options, Does.Contain("Rock Band Network 2"));
        Assert.That(options, Does.Not.Contain("RB1"));
        Assert.That(options, Does.Not.Contain("RBN2"));
    }

    [Test]
    public async Task Era_cell_has_a_tooltip_explaining_the_code()
    {
        // Column order at the default wide viewport: Song, Artist, Year, Genre, Era, ...
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
