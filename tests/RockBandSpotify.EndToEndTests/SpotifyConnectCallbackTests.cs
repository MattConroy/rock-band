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
        // Login now starts from the header, so it can be triggered from any
        // page — including one narrowed by a query string, which has to come
        // back intact.
        await Page.GotoAsync("/catalogue");
        await Page.EvaluateAsync(
            "localStorage.setItem('rock_band_owned_songs', JSON.stringify({ generatedAt: null, songIds: [4411, 98] }))");
        await Page.EvaluateAsync("localStorage.setItem('rock_band_pkce_return_path', 'catalogue?owned=1')");

        await Page.GotoAsync("/spotify-connect?code=fake&state=fake");

        await Expect(Page.GetByText("2 shown")).ToBeVisibleAsync(new() { Timeout = 15000 });
        Assert.That(Page.Url, Does.Contain("owned=1"));
    }

    [Test]
    public async Task Defaults_to_the_catalogue_when_no_return_path_is_stored()
    {
        await Page.GotoAsync("/spotify-connect?code=fake&state=fake");

        await Expect(Page.GetByText("4953 shown")).ToBeVisibleAsync(new() { Timeout = 15000 });
    }
}
