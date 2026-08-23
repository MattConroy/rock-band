using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using RockBandSpotify;
using RockBandSpotify.Models;
using RockBandSpotify.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var baseAddress = builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(baseAddress) });

// Bind configuration sections from wwwroot/appsettings.json.
var spotifyConfiguration = builder.Configuration.GetSection("Spotify").Get<SpotifyConfig>() ?? new SpotifyConfig();
var playStationConfiguration = builder.Configuration.GetSection("PlayStation").Get<PlayStationConfig>() ?? new PlayStationConfig();
var playlistConfiguration = builder.Configuration.GetSection("Playlist").Get<PlaylistConfig>() ?? new PlaylistConfig();
builder.Services.AddSingleton(spotifyConfiguration);
builder.Services.AddSingleton(playStationConfiguration);
builder.Services.AddSingleton(playlistConfiguration);

builder.Services.AddScoped(sp => new SpotifyAuthenticationService(
    sp.GetRequiredService<HttpClient>(),
    sp.GetRequiredService<IJSRuntime>(),
    sp.GetRequiredService<NavigationManager>(),
    spotifyConfiguration,
    baseAddress));

builder.Services.AddScoped<SpotifyApiService>();
builder.Services.AddScoped<ITrackLookup>(sp => sp.GetRequiredService<SpotifyApiService>());
builder.Services.AddScoped<OwnedLibrary>();
builder.Services.AddScoped<PlayStationService>();
builder.Services.AddScoped<MatchingService>();
builder.Services.AddScoped<PlaylistSyncService>();
builder.Services.AddScoped<CatalogueService>();
builder.Services.AddScoped<ConnectionState>();

await builder.Build().RunAsync();
