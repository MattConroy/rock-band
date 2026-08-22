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

    private ILocator Psn => Page.Locator(".conn-btn").First;
    private ILocator Spotify => Page.Locator(".conn-btn").Nth(1);

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
        await Expect(Page.Locator(".conn-btn")).ToHaveCountAsync(2);
    }

    [Test]
    public async Task They_start_disconnected()
    {
        await Expect(Psn).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("conn-disconnected"));
        await Expect(Spotify).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("conn-disconnected"));
        await Expect(Psn).ToHaveAttributeAsync("title", new System.Text.RegularExpressions.Regex("not connected"));
    }

    [Test]
    public async Task Pressing_a_disconnected_PlayStation_asks_for_a_token()
    {
        await Expect(Page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);

        await Psn.ClickAsync();

        await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
        await Expect(Page.GetByPlaceholder("npsso value, or the whole line")).ToBeVisibleAsync();
        // Nothing can be submitted until something is pasted.
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Connect", Exact = true })).ToBeDisabledAsync();
    }

    [Test]
    public async Task The_token_dialog_can_be_dismissed()
    {
        await Psn.ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);
    }

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

        await Psn.ClickAsync();
        var connect = Page.GetByRole(AriaRole.Button, new() { Name = "Connect", Exact = true });
        await Expect(connect).ToBeDisabledAsync();

        await Page.GetByPlaceholder("npsso value, or the whole line")
            .FillAsync("{\"npsso\":\"AbCd1234TOKEN\"}");

        // The dialog says what it understood, so a paste is never silent.
        await Expect(Page.Locator(".dialog-note")).ToContainTextAsync("Recognised a 13-character token");
        await Expect(connect).ToBeEnabledAsync();

        await connect.ClickAsync();
        await Expect(Page.Locator(".conn-error")).ToBeVisibleAsync();
        Assert.That(sent, Does.Contain("AbCd1234TOKEN"));
        Assert.That(sent, Does.Not.Contain("{\\"));
    }

    [Test]
    public async Task A_paste_that_is_not_a_token_cannot_be_submitted()
    {
        await Psn.ClickAsync();
        await Page.GetByPlaceholder("npsso value, or the whole line").FillAsync("npsso value goes here");

        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Connect", Exact = true })).ToBeDisabledAsync();
    }

    [Test]
    public async Task A_fetched_library_shows_as_synced()
    {
        await SeedLibrary(4411, 98);

        await Expect(Psn).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("conn-synced"));
        await Expect(Psn).ToHaveAttributeAsync("title", new System.Text.RegularExpressions.Regex("2 songs owned"));
    }

    [Test]
    public async Task Pressing_a_synced_PlayStation_narrows_the_catalogue()
    {
        await SeedLibrary(4411, 98); // Believer, Everlong

        await Psn.ClickAsync();

        await Expect(Page.GetByText("2 shown")).ToBeVisibleAsync();
        await Expect(Page.Locator("tbody")).ToContainTextAsync("Believer");
        await Expect(Page.Locator("tbody")).ToContainTextAsync("Everlong");
    }

    [Test]
    public async Task The_narrowed_view_is_in_the_address_and_survives_a_reload()
    {
        await SeedLibrary(4411, 98);
        await Psn.ClickAsync();
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

        await Psn.ClickAsync();
        await Page.GetByPlaceholder("npsso value, or the whole line").FillAsync("stale-token");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Connect", Exact = true }).ClickAsync();

        await Expect(Page.Locator(".conn-error")).ToContainTextAsync("npsso rejected by PlayStation");

        await Page.Locator(".conn-error-dismiss").ClickAsync();
        await Expect(Page.Locator(".conn-error")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Both_buttons_stay_on_screen_on_a_narrow_phone()
    {
        // The title is the only header item allowed to give up room; letting
        // it push the buttons past the right edge would strand them.
        await Page.SetViewportSizeAsync(320, 700);
        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync(new() { Timeout = 15000 });

        foreach (var button in await Page.Locator(".conn-btn").AllAsync())
        {
            var box = await button.BoundingBoxAsync();
            Assert.That(box, Is.Not.Null);
            Assert.That(box!.X, Is.GreaterThanOrEqualTo(0));
            Assert.That(box.X + box.Width, Is.LessThanOrEqualTo(320));
        }
    }

    [Test]
    public async Task No_console_errors_from_the_header()
    {
        var errors = new List<string>();
        Page.Console += (_, msg) => { if (msg.Type == "error") errors.Add(msg.Text); };

        await Psn.ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
        await SeedLibrary(4411);
        await Psn.ClickAsync();

        Assert.That(errors, Is.Empty);
    }
}
