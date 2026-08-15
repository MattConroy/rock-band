using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace RockBandSpotify.EndToEndTests;

/// <summary>
/// Base for browser tests: points at the running app (started by the caller —
/// see the CI workflow or README) and exposes the app's base URL.
///
/// Honors PLAYWRIGHT_CHROMIUM_PATH to launch a specific Chromium binary
/// instead of the auto-installed one. Not needed in CI (which installs the
/// matching browser via `playwright install`); useful for local runs against
/// a pre-existing Chromium install without network access to Playwright's CDN.
/// </summary>
public abstract class AppPageTest : PageTest
{
    protected static string BaseUrl =>
        Environment.GetEnvironmentVariable("APP_BASE_URL") ?? "http://localhost:5010";

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        BaseURL = BaseUrl,
    };

    public override Task<BrowserTypeLaunchOptions?> LaunchOptionsAsync()
    {
        var options = new BrowserTypeLaunchOptions();
        var executablePath = Environment.GetEnvironmentVariable("PLAYWRIGHT_CHROMIUM_PATH");
        if (!string.IsNullOrEmpty(executablePath))
            options.ExecutablePath = executablePath;
        return Task.FromResult<BrowserTypeLaunchOptions?>(options);
    }
}
