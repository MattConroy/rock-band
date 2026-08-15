using Microsoft.Playwright;

namespace RockBandSpotify.EndToEndTests;

/// <summary>
/// Covers the /spotify-connect redirect handoff without touching the real
/// Spotify API: omitting the PKCE verifier from localStorage makes
/// TryCompleteLoginAsync short-circuit before any network call, so these
/// tests exercise only the return-path routing.
/// </summary>
public class SpotifyConnectCallbackTests : AppPageTest
{
    [Test]
    public async Task Returns_to_where_login_was_initiated()
    {
        await Page.GotoAsync("/connect");
        await Page.EvaluateAsync("localStorage.setItem('rb_pkce_return_path', 'connect')");

        await Page.GotoAsync("/spotify-connect?code=fake&state=fake");

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Connect Spotify" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Defaults_to_the_catalogue_when_no_return_path_is_stored()
    {
        await Page.GotoAsync("/spotify-connect?code=fake&state=fake");

        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync(new() { Timeout = 15000 });
    }
}
