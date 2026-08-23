namespace RockBandSpotify.Models;

public class SpotifyConfig
{
    public string ClientId { get; set; } = "";
    public string Scopes { get; set; } = "playlist-modify-public playlist-modify-private";

    /// <summary>
    /// Whether to search Spotify for songs the catalogue has no track id for.
    ///
    /// <para>
    /// Off by default. Searching costs one request per unknown song and returns
    /// a guess that has to be reviewed, and a large library can spend that
    /// budget fast enough to earn a rate limit measured in hours. The songs the
    /// catalogue does know are unaffected either way — those are fetched by id.
    /// </para>
    /// </summary>
    public bool SearchForMissingTracks { get; set; }
}

public class PlayStationConfig
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
