using Microsoft.Playwright;

namespace RockBandSpotify.EndToEndTests;

/// <summary>
/// The two header buttons: what they show, and what pressing them does at each
/// stage. Spotify's own steps need a real account, so only its disconnected
/// state is covered here.
/// </summary>
public class ConnectionButtonTests : AppPageTest
{
    [SetUp]
    public async Task GotoCatalogue()
    {
        await Page.GotoAsync("/catalogue");
        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync(new() { Timeout = 15000 });
    }

    private ILocator PlayStation => Page.Locator(".connection-button").First;
    private ILocator Spotify => Page.Locator(".connection-button").Nth(1);

    private async Task SeedLibrary(params int[] songIds)
    {
        await Page.EvaluateAsync(
            "ids => localStorage.setItem('rb_owned_songs', JSON.stringify({ generatedAt: null, songIds: ids }))",
            songIds);
        await Page.ReloadAsync();
        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync(new() { Timeout = 15000 });
    }

    [Test]
    public async Task Both_connections_appear_in_the_header()
    {
        await Expect(Page.Locator(".connection-button")).ToHaveCountAsync(2);
    }

    [Test]
    public async Task They_start_disconnected()
    {
        await Expect(PlayStation).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("connection-disconnected"));
        await Expect(Spotify).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("connection-disconnected"));
        await Expect(PlayStation).ToHaveAttributeAsync("title", new System.Text.RegularExpressions.Regex("not connected"));
    }

    [Test]
    public async Task Pressing_a_disconnected_PlayStation_asks_for_a_token()
    {
        await Expect(Page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);

        await PlayStation.ClickAsync();

        await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
        await Expect(Page.GetByPlaceholder("npsso value, or the whole line")).ToBeVisibleAsync();
        // Nothing can be submitted until something is pasted.
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Connect", Exact = true })).ToBeDisabledAsync();
    }

    [Test]
    public async Task The_token_dialog_can_be_dismissed()
    {
        await PlayStation.ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);
    }

    /// <summary>A token of the length Sony issues, so validation accepts it.</summary>
    private const string ValidToken = "AbCd1234567890TOKENvalueQwErTyUiOpAsDfGhJkLzXcVbNm0987654321_x-Y";

    [Test]
    public async Task The_whole_json_line_can_be_pasted()
    {
        // Copying the line off Sony's page is easier than selecting the value
        // inside the quotes, so that is what most pastes look like.
        string? sent = null;
        await Page.RouteAsync("**rockband-psn-gateway**", async route =>
        {
            sent = route.Request.PostData;
            await route.FulfillAsync(new() { Status = 401, ContentType = "application/json", Body = "{}" });
        });

        await PlayStation.ClickAsync();
        var connect = Page.GetByRole(AriaRole.Button, new() { Name = "Connect", Exact = true });
        await Expect(connect).ToBeDisabledAsync();

        await Page.GetByPlaceholder("npsso value, or the whole line")
            .FillAsync($"{{\"npsso\":\"{ValidToken}\"}}");

        // The dialog says what it understood, so a paste is never silent.
        await Expect(Page.Locator(".dialog-note")).ToContainTextAsync("Looks right");
        await Expect(connect).ToBeEnabledAsync();

        await connect.ClickAsync();
        await Expect(Page.Locator(".connection-toast-bad")).ToBeVisibleAsync();
        Assert.That(sent, Does.Contain(ValidToken));
        Assert.That(sent, Does.Not.Contain("{\\"));
    }

    [Test]
    public async Task A_paste_that_is_not_a_token_cannot_be_submitted()
    {
        await PlayStation.ClickAsync();
        await Page.GetByPlaceholder("npsso value, or the whole line").FillAsync("npsso value goes here");

        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Connect", Exact = true })).ToBeDisabledAsync();
    }

    [Test]
    public async Task The_field_marks_itself_valid_or_not_as_you_type()
    {
        await PlayStation.ClickAsync();
        var field = Page.GetByPlaceholder("npsso value, or the whole line");

        // Nothing typed yet is neither state — no mark, no coloured outline.
        await Expect(Page.Locator(".field-mark")).ToHaveCountAsync(0);
        await Expect(field).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("field-(ok|bad)"));

        await field.FillAsync(ValidToken);
        await Expect(field).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("field-ok"));
        await Expect(Page.Locator(".field-mark.field-ok")).ToBeVisibleAsync();

        // Losing a character makes it wrong again, and says why.
        await field.FillAsync(ValidToken[..40]);
        await Expect(field).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("field-bad"));
        await Expect(Page.Locator(".field-mark.field-bad")).ToBeVisibleAsync();
        await Expect(Page.Locator(".dialog-note")).ToContainTextAsync("this is 40");

        // And clearing it returns to neither state rather than staying red.
        await field.FillAsync("");
        await Expect(Page.Locator(".field-mark")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task A_truncated_token_is_never_sent_to_the_gateway()
    {
        var calls = 0;
        await Page.RouteAsync("**rockband-psn-gateway**", async route =>
        {
            calls++;
            await route.FulfillAsync(new() { Status = 401, ContentType = "application/json", Body = "{}" });
        });

        await PlayStation.ClickAsync();
        await Page.GetByPlaceholder("npsso value, or the whole line").FillAsync(ValidToken[..40]);
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator(".dialog-note")).ToContainTextAsync("this is 40");
        Assert.That(calls, Is.Zero, "a token the app knows is malformed should not reach PlayStation");
    }

    [Test]
    public async Task A_fetched_library_shows_as_synced()
    {
        await SeedLibrary(4411, 98);

        await Expect(PlayStation).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("connection-synced"));
        await Expect(PlayStation).ToHaveAttributeAsync("title", new System.Text.RegularExpressions.Regex("2 songs owned"));
    }

    [Test]
    public async Task Pressing_a_synced_PlayStation_shows_what_was_fetched()
    {
        await SeedLibrary(4411, 98); // Believer, Everlong

        await PlayStation.ClickAsync();

        await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
        await Expect(Page.Locator(".stat").First).ToContainTextAsync("2");
    }

    [Test]
    public async Task The_owned_filter_can_be_turned_on_and_back_off()
    {
        // It used to navigate to the address it was already on, so a second
        // press did nothing and there was no way back to the full catalogue.
        await SeedLibrary(4411, 98);

        await PlayStation.ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Show only songs I own" }).ClickAsync();
        await Expect(Page.GetByText("2 shown")).ToBeVisibleAsync();
        await Expect(Page.Locator("tbody")).ToContainTextAsync("Believer");

        await PlayStation.ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Show the whole catalogue" }).ClickAsync();
        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync();

        // And again, to prove it isn't a one-shot.
        await PlayStation.ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Show only songs I own" }).ClickAsync();
        await Expect(Page.GetByText("2 shown")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Clearing_the_filters_updates_the_button()
    {
        // The page and the header both decided whether the catalogue was
        // narrowed, and clearing here left owned=1 in the address — so the
        // dialog offered to undo a filter that was already gone, and its
        // opposite did nothing.
        await SeedLibrary(4411, 98);
        await PlayStation.ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Show only songs I own" }).ClickAsync();
        await Expect(Page.GetByText("2 shown")).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Clear filters" }).ClickAsync();
        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync();

        await PlayStation.ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Show only songs I own" })).ToBeVisibleAsync();

        // And the filter is still reachable rather than stuck.
        await Page.GetByRole(AriaRole.Button, new() { Name = "Show only songs I own" }).ClickAsync();
        await Expect(Page.GetByText("2 shown")).ToBeVisibleAsync();
    }

    [Test]
    public async Task The_owned_dropdown_and_the_button_agree()
    {
        await SeedLibrary(4411, 98);

        // Narrowing from the page's own dropdown, not the dialog.
        await Page.GetByLabel("Ownership").SelectOptionAsync("Owned");
        await Expect(Page.GetByText("2 shown")).ToBeVisibleAsync();

        await PlayStation.ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Show the whole catalogue" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task A_success_is_reported_as_well_as_a_failure()
    {
        // A sync that adds nothing and one that adds hundreds used to look
        // identical from the outside — silence either way.
        await SeedLibrary(4411, 98);
        await PlayStation.ClickAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Clear fetched songs" }).ClickAsync();

        await Expect(Page.Locator(".connection-toast-good")).ToContainTextAsync("Cleared the fetched songs");
        await Page.Locator(".connection-toast-dismiss").ClickAsync();
        await Expect(Page.Locator(".connection-toast-good")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Clearing_the_songs_keeps_the_sign_in()
    {
        await SeedLibrary(4411, 98);
        await PlayStation.ClickAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Clear fetched songs" }).ClickAsync();

        // Back to connected — amber, not disconnected — so no new token is needed.
        await Expect(PlayStation).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("connection-connected"));
        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Disconnecting_forgets_the_token_too()
    {
        await SeedLibrary(4411, 98);
        await PlayStation.ClickAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Disconnect PlayStation" }).ClickAsync();

        await Expect(PlayStation).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("connection-disconnected"));

        // And the next press asks for a token again rather than a status.
        await PlayStation.ClickAsync();
        await Expect(Page.GetByPlaceholder("npsso value, or the whole line")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Leaving_the_filter_on_does_not_strand_an_empty_catalogue()
    {
        // Disconnecting while narrowed would otherwise leave the page filtered
        // to a library that no longer exists.
        await SeedLibrary(4411, 98);
        await PlayStation.ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Show only songs I own" }).ClickAsync();
        await Expect(Page.GetByText("2 shown")).ToBeVisibleAsync();

        await PlayStation.ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Disconnect PlayStation" }).ClickAsync();

        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync();
    }

    [Test]
    public async Task The_narrowed_view_is_in_the_address_and_survives_a_reload()
    {
        await SeedLibrary(4411, 98);
        await PlayStation.ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Show only songs I own" }).ClickAsync();
        await Expect(Page.GetByText("2 shown")).ToBeVisibleAsync();

        Assert.That(Page.Url, Does.Contain("owned=1"));

        await Page.ReloadAsync();
        await Expect(Page.GetByText("2 shown")).ToBeVisibleAsync(new() { Timeout = 15000 });
    }

    [Test]
    public async Task The_connect_page_is_gone()
    {
        // Its work moved into the header, and the SPA fallback would otherwise
        // quietly render the catalogue at a stale bookmark.
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Connect" })).ToHaveCountAsync(0);
    }

    [Test]
    public async Task A_failure_is_shown_in_the_header_and_can_be_dismissed()
    {
        // A rejected npsso is the most common real failure. It used to be
        // stored and never rendered, which made a failed press look exactly
        // like a button that did nothing.
        await Page.RouteAsync("**rockband-psn-gateway**", route => route.FulfillAsync(new()
        {
            Status = 401,
            ContentType = "application/json",
            Body = "{\"error\":\"npsso rejected by PlayStation\"}",
        }));

        await PlayStation.ClickAsync();
        await Page.GetByPlaceholder("npsso value, or the whole line").FillAsync(ValidToken);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Connect", Exact = true }).ClickAsync();

        await Expect(Page.Locator(".connection-toast-bad")).ToContainTextAsync("npsso rejected by PlayStation");

        await Page.Locator(".connection-toast-dismiss").ClickAsync();
        await Expect(Page.Locator(".connection-toast-bad")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Both_buttons_stay_on_screen_on_a_narrow_phone()
    {
        // The title is the only header item allowed to give up room; letting
        // it push the buttons past the right edge would strand them.
        await Page.SetViewportSizeAsync(320, 700);
        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync(new() { Timeout = 15000 });

        foreach (var button in await Page.Locator(".connection-button").AllAsync())
        {
            var box = await button.BoundingBoxAsync();
            Assert.That(box, Is.Not.Null);
            Assert.That(box!.X, Is.GreaterThanOrEqualTo(0));
            Assert.That(box.X + box.Width, Is.LessThanOrEqualTo(320));
        }
    }

    /// <summary>Signed in, with a playlist recorded from an earlier sync.</summary>
    private async Task SeedSpotify(int trackCount)
    {
        await Page.EvaluateAsync(
            @"count => {
                localStorage.setItem('rb_spotify_token', JSON.stringify({
                    AccessToken: 'fake', ExpiresAt: '2099-01-01T00:00:00+00:00',
                    Scope: 'playlist-read-private playlist-modify-public playlist-modify-private' }));
                localStorage.setItem('rb_spotify_playlist', JSON.stringify({
                    Url: 'https://open.spotify.com/playlist/xyz', Name: 'Rock Band DLC', TrackCount: count }));
            }", trackCount);
        await Page.ReloadAsync();
        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync(new() { Timeout = 15000 });
    }

    [Test]
    public async Task A_synced_Spotify_offers_a_resync_not_just_the_playlist()
    {
        // Opening the playlist was all it did, which is a dead end the moment
        // the playlist is wrong — as it was when a sync produced an empty one.
        await SeedLibrary(4411, 98);
        await SeedSpotify(trackCount: 0);

        await Spotify.ClickAsync();

        await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Sync now" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Open in Spotify ↗" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task An_empty_playlist_is_visible_as_empty()
    {
        await SeedLibrary(4411, 98);
        await SeedSpotify(trackCount: 0);

        // The count is on the button itself, so it doesn't take opening the
        // playlist in another tab to notice nothing is in it.
        await Expect(Spotify).ToHaveAttributeAsync("title",
            new System.Text.RegularExpressions.Regex("0 songs in Rock Band DLC"));

        await Spotify.ClickAsync();
        await Expect(Page.Locator(".dialog-intro")).ToContainTextAsync("2 of your songs aren't in Rock Band DLC yet");
    }

    [Test]
    public async Task A_playlist_stored_by_an_older_build_still_counts_as_synced()
    {
        // Earlier builds saved just the URL. Reading those keeps a sync that
        // already happened rather than silently demoting it.
        await Page.EvaluateAsync(
            "localStorage.setItem('rb_spotify_token', JSON.stringify({ AccessToken: 'fake', ExpiresAt: '2099-01-01T00:00:00+00:00', Scope: 'playlist-read-private playlist-modify-public playlist-modify-private' }));"
            + "localStorage.setItem('rb_spotify_playlist', JSON.stringify('https://open.spotify.com/playlist/old'));");
        await Page.ReloadAsync();
        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync(new() { Timeout = 15000 });

        await Expect(Spotify).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("connection-synced"));
    }

    [Test]
    public async Task No_console_errors_from_the_header()
    {
        var errors = new List<string>();
        Page.Console += (_, msg) => { if (msg.Type == "error") errors.Add(msg.Text); };

        await PlayStation.ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
        await SeedLibrary(4411);
        await PlayStation.ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Show only songs I own" }).ClickAsync();

        Assert.That(errors, Is.Empty);
    }
}
