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
        await Expect(Page.GetByPlaceholder("Paste the npsso value")).ToBeVisibleAsync();
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
