namespace RockBandSpotify.Models;

public class SpotifyConfig
{
    public string ClientId { get; set; } = "";
    public string Scopes { get; set; } = "playlist-modify-public playlist-modify-private";
}

public class PsnConfig
{
    public string GatewayUrl { get; set; } = "";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(GatewayUrl)
                                && !GatewayUrl.StartsWith("REPLACE_");
}

public class PlaylistConfig
{
    public string Name { get; set; } = "Rock Band DLC";
    public string Description { get; set; } = "";
    public bool Public { get; set; }
}
