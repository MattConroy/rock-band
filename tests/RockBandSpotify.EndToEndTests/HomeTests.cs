namespace RockBandSpotify.EndToEndTests;

/// <summary>Smoke test for the original Spotify/PSN connect flow, so the
/// layout change made for the catalogue page can't silently break it.</summary>
public class HomeTests : AppPageTest
{
    [Test]
    public async Task Loads_the_three_step_workflow_without_console_errors()
    {
        var errors = new List<string>();
        Page.Console += (_, msg) =>
        {
            if (msg.Type == "error") errors.Add(msg.Text);
        };

        await Page.GotoAsync("/");

        await Expect(Page.GetByRole(Microsoft.Playwright.AriaRole.Heading, new() { Name = "Connect Spotify" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(Microsoft.Playwright.AriaRole.Heading, new() { Name = "Connect PlayStation" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(Microsoft.Playwright.AriaRole.Heading, new() { Name = "Match to Spotify tracks" })).ToBeVisibleAsync();

        Assert.That(errors, Is.Empty);
    }

    [Test]
    public async Task Nav_link_reaches_the_catalogue()
    {
        await Page.GotoAsync("/");
        await Page.GetByRole(Microsoft.Playwright.AriaRole.Link, new() { Name = "Catalogue" }).ClickAsync();
        await Expect(Page.GetByText("4960 shown")).ToBeVisibleAsync();
    }
}
