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
var spotifyConfig = builder.Configuration.GetSection("Spotify").Get<SpotifyConfig>() ?? new SpotifyConfig();
var psnConfig = builder.Configuration.GetSection("Psn").Get<PsnConfig>() ?? new PsnConfig();
var playlistConfig = builder.Configuration.GetSection("Playlist").Get<PlaylistConfig>() ?? new PlaylistConfig();
builder.Services.AddSingleton(spotifyConfig);
builder.Services.AddSingleton(psnConfig);
builder.Services.AddSingleton(playlistConfig);

builder.Services.AddScoped(sp => new SpotifyAuthService(
    sp.GetRequiredService<HttpClient>(),
    sp.GetRequiredService<IJSRuntime>(),
    sp.GetRequiredService<NavigationManager>(),
    spotifyConfig,
    baseAddress));

builder.Services.AddScoped<SpotifyApiService>();
builder.Services.AddScoped<ITrackLookup>(sp => sp.GetRequiredService<SpotifyApiService>());
builder.Services.AddScoped<OwnedLibrary>();
builder.Services.AddScoped<PsnService>();
builder.Services.AddScoped<MatchingService>();
builder.Services.AddScoped<PlaylistSyncService>();
builder.Services.AddScoped<CatalogueService>();

await builder.Build().RunAsync();
