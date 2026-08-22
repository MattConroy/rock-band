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
        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync(new() { Timeout = 15000 });
    }

    [Test]
    public async Task Loads_the_full_catalogue()
    {
        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync(new() { Timeout = 15000 });
    }

    [Test]
    public async Task Root_path_also_shows_the_catalogue()
    {
        await Page.GotoAsync("/");
        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync(new() { Timeout = 15000 });
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
        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync(new() { Timeout = 15000 });

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
    public async Task Release_date_is_available_from_the_column_picker()
    {
        // Off by default, so the standard view stays four columns wide.
        var headers = await Page.Locator("th").AllInnerTextsAsync();
        Assert.That(headers, Does.Not.Contain("RELEASED"));

        await Page.GetByLabel("Choose columns").ClickAsync();
        await Page.GetByRole(AriaRole.Checkbox, new() { Name = "Released" }).CheckAsync();

        headers = await Page.Locator("th").AllInnerTextsAsync();
        Assert.That(headers, Is.EqualTo(new[] { "SONG", "ARTIST", "YEAR", "GENRE", "SOURCE", "RELEASED" }));

        await Page.GetByPlaceholder("Search song or artist…").FillAsync("believer");
        await Expect(Page.GetByText("1 shown")).ToBeVisibleAsync();
        // Believer is a 2017 song that reached Rock Band on 2018-03-01, which
        // is the point of having this column as well as Year.
        await Expect(Page.Locator("tbody td").Nth(5)).ToHaveTextAsync("1 Mar 2018");
        await Expect(Page.Locator("tbody td").Nth(2)).ToHaveTextAsync("2017");
    }

    [Test]
    public async Task Sorting_by_release_date_differs_from_sorting_by_year()
    {
        await Page.GetByLabel("Choose columns").ClickAsync();
        await Page.GetByRole(AriaRole.Checkbox, new() { Name = "Released" }).CheckAsync();
        await Page.GetByLabel("Choose columns").ClickAsync();

        var released = Page.Locator("th").Nth(5);
        await released.Locator("button").ClickAsync();
        await Expect(released).ToHaveAttributeAsync("aria-sort", "ascending");

        // The catalogue starts with Rock Band 1's launch day.
        await Expect(Page.Locator("tbody tr").First.Locator("td").Nth(5)).ToHaveTextAsync("20 Nov 2007");

        await released.Locator("button").ClickAsync();
        await Expect(released).ToHaveAttributeAsync("aria-sort", "descending");
        var newest = await Page.Locator("tbody tr").First.Locator("td").Nth(5).InnerTextAsync();
        Assert.That(newest, Does.Contain("202"));
    }

    [Test]
    public async Task Column_choice_survives_a_reload()
    {
        await Page.GetByLabel("Choose columns").ClickAsync();
        await Page.GetByRole(AriaRole.Checkbox, new() { Name = "Year" }).UncheckAsync();

        await Page.ReloadAsync();
        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync(new() { Timeout = 15000 });

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
    public async Task Multi_source_song_lists_every_game_it_shipped_in()
    {
        // Everlong shipped on the Rock Band 2 disc and again in Unplugged.
        await Page.GetByPlaceholder("Search song or artist…").FillAsync("everlong");
        await Expect(Page.GetByText("1 shown")).ToBeVisibleAsync();

        // Column order at the default wide viewport: Song, Artist, Year, Genre, Source.
        var cell = Page.Locator("tbody td").Nth(4);
        await Expect(cell).ToContainTextAsync("Rock Band 2");
        await Expect(cell).ToContainTextAsync("Rock Band Unplugged");
    }

    [Test]
    public async Task Filtering_by_a_non_origin_source_still_finds_the_song()
    {
        // Everlong's origin is Rock Band 2, but filtering to Unplugged must find
        // it too — membership, not just origin.
        await Page.Locator("select").Nth(1).SelectOptionAsync(new SelectOptionValue { Value = "UNPLUGGED" });
        await Page.GetByPlaceholder("Search song or artist…").FillAsync("everlong");
        await Expect(Page.GetByText("1 shown")).ToBeVisibleAsync();
        await Expect(Page.Locator("tbody")).ToContainTextAsync("Everlong");
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
        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync(new() { Timeout = 15000 });
    }

    [Test]
    public async Task Only_the_grid_scrolls_not_the_page()
    {
        var pageScrollable = await Page.EvaluateAsync<bool>(
            "document.documentElement.scrollHeight > document.documentElement.clientHeight + 1");
        Assert.That(pageScrollable, Is.False, "the outer page should never scroll on the catalogue view");

        var wrapScrollable = await Page.EvalOnSelectorAsync<bool>(
            ".table-body-wrap", "el => el.scrollHeight > el.clientHeight");
        Assert.That(wrapScrollable, Is.True, "the grid body should be the scrolling region");
    }

    [Test]
    public async Task Column_widths_stay_stable_while_scrolling()
    {
        // table-layout: fixed with declared widths keeps columns from ever
        // being recalculated from content — a permanent guard against this
        // regressing, since a from-content ("auto") layout jumping around
        // during scroll is exactly what motivated this in the first place.
        var headerWidths = await Page.Locator("th").AllAsync();
        var before = new List<float>();
        foreach (var h in headerWidths)
            before.Add((await h.BoundingBoxAsync())!.Width);

        await Page.EvalOnSelectorAsync(".table-body-wrap", "el => el.scrollTop = 40000");
        await Page.WaitForTimeoutAsync(200);

        var after = new List<float>();
        foreach (var h in headerWidths)
            after.Add((await h.BoundingBoxAsync())!.Width);

        Assert.That(after, Is.EqualTo(before), "column widths should not change after scrolling");
    }

    [Test]
    public async Task Row_heights_flex_to_content_instead_of_being_forced_uniform()
    {
        // A single-line row should stay compact, and a row whose text wraps
        // should grow to fit it rather than every row being padded out to
        // the tallest possible height.
        var compact = (await Page.Locator("tbody tr").First.BoundingBoxAsync())!.Height;
        Assert.That(compact, Is.LessThan(40), "a normal single-line row should stay compact");

        // The longest song title in the catalogue (92 characters) reliably
        // wraps within the Song column's width.
        await Page.GetByPlaceholder("Search song or artist…").FillAsync("It's Better to Spend Money");
        await Expect(Page.GetByText("1 shown")).ToBeVisibleAsync();

        var wrapped = (await Page.Locator("tbody tr").First.BoundingBoxAsync())!.Height;
        Assert.That(wrapped, Is.GreaterThan(40), "a row with a long wrapped title should grow taller");
    }

    [Test]
    public async Task Table_uses_separate_borders_not_collapsed()
    {
        // A border shared between the sticky thead and the first tbody row
        // (border-collapse: collapse) breaks once that row scrolls out from
        // under the header: the divider vanishes and row content can paint
        // above the sticky header. Separate borders render independently of
        // scroll position.
        var borderCollapse = await Page.EvalOnSelectorAsync<string>(
            ".catalogue-table", "el => getComputedStyle(el).borderCollapse");
        Assert.That(borderCollapse, Is.EqualTo("separate"));
    }

    [Test]
    public async Task Tapping_a_column_header_cycles_unsorted_ascending_descending_unsorted()
    {
        var songTh = Page.Locator("th").First;
        var songHeaderButton = songTh.Locator("button");
        var firstCell = Page.Locator("tbody tr").First.Locator("td").First;

        var originalFirst = await firstCell.InnerTextAsync();

        await songHeaderButton.ClickAsync();
        await Expect(songTh).ToHaveAttributeAsync("aria-sort", "ascending");
        var ascFirst = await firstCell.InnerTextAsync();
        Assert.That(ascFirst, Is.Not.EqualTo(originalFirst));

        await songHeaderButton.ClickAsync();
        await Expect(songTh).ToHaveAttributeAsync("aria-sort", "descending");
        var descFirst = await firstCell.InnerTextAsync();
        Assert.That(descFirst, Is.Not.EqualTo(ascFirst));

        await songHeaderButton.ClickAsync();
        Assert.That(await songTh.GetAttributeAsync("aria-sort"), Is.Null);
        Assert.That(await firstCell.InnerTextAsync(), Is.EqualTo(originalFirst));
    }

    [Test]
    public async Task Tapping_a_different_column_resets_the_previous_one()
    {
        var songTh = Page.Locator("th").First;
        var artistTh = Page.Locator("th").Nth(1);

        await songTh.Locator("button").ClickAsync();
        await Expect(songTh).ToHaveAttributeAsync("aria-sort", "ascending");

        await artistTh.Locator("button").ClickAsync();
        await Expect(artistTh).ToHaveAttributeAsync("aria-sort", "ascending");
        Assert.That(await songTh.GetAttributeAsync("aria-sort"), Is.Null);
    }

    // The ownership controls only exist once PlayStation has been fetched, so
    // these seed the stored set the way a real fetch would leave it.
    private async Task SeedOwned(params int[] songIds)
    {
        await Page.EvaluateAsync(
            "ids => localStorage.setItem('rb_owned_song_ids', JSON.stringify(ids))", songIds);
        await Page.ReloadAsync();
        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync(new() { Timeout = 15000 });
    }

    [Test]
    public async Task No_ownership_filter_until_a_library_has_been_fetched()
    {
        await Expect(Page.GetByLabel("Ownership")).Not.ToBeVisibleAsync();
        await Expect(Page.Locator(".owned-tick")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Owned_songs_are_ticked_in_the_grid()
    {
        await SeedOwned(4411); // Believer — Imagine Dragons

        await Page.GetByPlaceholder("Search song or artist…").FillAsync("believer");
        await Expect(Page.GetByText("1 shown")).ToBeVisibleAsync();
        await Expect(Page.Locator("tbody tr").First.Locator("td.col-owned .owned-tick")).ToBeVisibleAsync();

        await Page.GetByPlaceholder("Search song or artist…").FillAsync("everlong");
        await Expect(Page.GetByText("1 shown")).ToBeVisibleAsync();
        await Expect(Page.Locator("tbody tr").First.Locator(".owned-tick")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Ownership_is_its_own_column_once_a_library_exists()
    {
        var before = await Page.Locator("th").AllInnerTextsAsync();
        Assert.That(before, Is.EqualTo(new[] { "SONG", "ARTIST", "YEAR", "GENRE", "SOURCE" }));

        await SeedOwned(4411);

        var after = await Page.Locator("th").AllInnerTextsAsync();
        Assert.That(after, Is.EqualTo(new[] { "OWN", "SONG", "ARTIST", "YEAR", "GENRE", "SOURCE" }));

        // The tick sits in its own cell, not crammed in front of the title.
        await Page.GetByPlaceholder("Search song or artist…").FillAsync("believer");
        await Expect(Page.GetByText("1 shown")).ToBeVisibleAsync();
        var song = await Page.Locator("tbody tr").First.Locator("td.col-song").InnerTextAsync();
        Assert.That(song.Trim(), Is.EqualTo("Believer"));
    }

    [Test]
    public async Task Sorting_by_ownership_brings_your_library_to_the_top()
    {
        await SeedOwned(4411, 98); // Believer, Everlong

        await Page.Locator("th.col-owned button").ClickAsync();
        await Expect(Page.Locator("th.col-owned")).ToHaveAttributeAsync("aria-sort", "ascending");

        // The first two rows are the owned ones, in some order.
        var top = await Page.Locator("tbody tr").Nth(0).Locator("td.col-song").InnerTextAsync();
        var second = await Page.Locator("tbody tr").Nth(1).Locator("td.col-song").InnerTextAsync();
        Assert.That(new[] { top.Trim(), second.Trim() }, Is.EquivalentTo(new[] { "Believer", "Everlong" }));

        var third = Page.Locator("tbody tr").Nth(2).Locator(".owned-tick");
        await Expect(third).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Filter_controls_stay_inside_the_card_on_a_phone()
    {
        await SeedOwned(4411);
        await Page.SetViewportSizeAsync(390, 780); // iPhone-ish
        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync(new() { Timeout = 15000 });

        // Every select has to sit within the card, not hang off its edge.
        var cardRight = (await Page.Locator(".rb-card").First.BoundingBoxAsync())!.X
                        + (await Page.Locator(".rb-card").First.BoundingBoxAsync())!.Width;
        foreach (var sel in await Page.Locator(".select-group .rb-select").AllAsync())
        {
            var box = (await sel.BoundingBoxAsync())!;
            Assert.That(box.X + box.Width, Is.LessThanOrEqualTo(cardRight + 1),
                "a filter control is overflowing the card on a narrow viewport");
        }

        var pageScrollsSideways = await Page.EvaluateAsync<bool>(
            "document.documentElement.scrollWidth > document.documentElement.clientWidth + 1");
        Assert.That(pageScrollsSideways, Is.False, "the page should never scroll horizontally");
    }

    [Test]
    public async Task Filtering_to_owned_narrows_the_grid_to_the_fetched_library()
    {
        await SeedOwned(4411, 98); // Believer, Everlong

        await Page.GetByLabel("Ownership").SelectOptionAsync("Owned");
        await Expect(Page.GetByText("2 shown")).ToBeVisibleAsync();
        await Expect(Page.Locator("tbody")).ToContainTextAsync("Believer");
        await Expect(Page.Locator("tbody")).ToContainTextAsync("Everlong");
        // Short labels that echo the column header, and a first option that
        // honestly describes what it shows — the whole catalogue, not a
        // purchase history.
        var options = await Page.GetByLabel("Ownership").Locator("option").AllInnerTextsAsync();
        Assert.That(options, Is.EqualTo(new[] { "All songs", "Owned", "Unowned" }));

        await Page.GetByLabel("Ownership").SelectOptionAsync("NotOwned");
        await Expect(Page.GetByText("4951 shown")).ToBeVisibleAsync();

        // Imagine Dragons have other songs in the catalogue, so the artist is
        // still on screen — it's the owned title that has to be gone.
        await Page.GetByPlaceholder("Search song or artist…").FillAsync("believer");
        await Expect(Page.GetByText("0 shown")).ToBeVisibleAsync();
    }

    [Test]
    public async Task The_ownership_choice_survives_a_reload_without_refetching()
    {
        await SeedOwned(4411, 98);
        await Page.ReloadAsync();
        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync(new() { Timeout = 15000 });

        // The library is read back from storage, so the ticks are there with no
        // PlayStation call in between.
        await Page.GetByLabel("Ownership").SelectOptionAsync("Owned");
        await Expect(Page.GetByText("2 shown")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Clear_filters_resets_ownership_too()
    {
        await SeedOwned(4411);
        await Page.GetByLabel("Ownership").SelectOptionAsync("Owned");
        await Expect(Page.GetByText("1 shown")).ToBeVisibleAsync();

        await Page.GetByText("Clear filters").ClickAsync();
        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync(new() { Timeout = 15000 });
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
