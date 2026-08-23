using Microsoft.AspNetCore.Components;
using RockBandSpotify.Services;

namespace RockBandSpotify.Pages;

/// <summary>
/// Dedicated Spotify OAuth redirect target — the only page whose job is
/// completing the token exchange, then sending the user back to wherever
/// they triggered login from.
/// </summary>
public partial class SpotifyConnectCallback
{
    [Inject] private SpotifyAuthenticationService Authentication { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Authentication.TryCompleteLoginAsync(Navigation.Uri);
        var returnPath = await Authentication.ConsumeReturnPathAsync();
        Navigation.NavigateTo(returnPath);
    }
}
