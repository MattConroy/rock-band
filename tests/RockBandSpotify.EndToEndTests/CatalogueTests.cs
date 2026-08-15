using Microsoft.Playwright;

namespace RockBandSpotify.EndToEndTests;

/// <summary>
/// Browser tests for the standalone /catalogue page — the parts that only a
/// real rendered page can prove: filtering, selection, layout.
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
    public async Task Table_has_no_RB4_column()
    {
        // Headers render uppercase via CSS text-transform, which shows up in innerText.
        var headers = await Page.Locator("th").AllInnerTextsAsync();
        Assert.That(headers, Does.Not.Contain("RB4"));
        Assert.That(headers, Is.EqualTo(new[] { "", "SONG", "ARTIST", "YEAR", "GENRE", "ERA" }));
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
        var cell = Page.Locator("tbody td").Nth(5);
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
    public async Task Selecting_a_song_updates_the_count_and_survives_search_changes()
    {
        await Page.GetByPlaceholder("Search song or artist…").FillAsync("believer");
        await Page.Locator("tbody input[type=checkbox]").First.CheckAsync();
        await Expect(Page.GetByText("1 selected")).ToBeVisibleAsync();

        await Page.GetByPlaceholder("Search song or artist…").FillAsync("");
        await Page.GetByText("Selected only").ClickAsync();

        await Expect(Page.GetByText("1 shown")).ToBeVisibleAsync();
        await Expect(Page.GetByText("1 selected")).ToBeVisibleAsync();
        await Expect(Page.Locator("tbody")).ToContainTextAsync("Believer");
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
