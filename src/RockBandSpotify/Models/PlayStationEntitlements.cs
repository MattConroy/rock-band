using System.Text.Json.Serialization;

namespace RockBandSpotify.Models;

/// <summary>
/// What the gateway Worker returns (see gateway/worker.javaScript): the raw content
/// codes an account owns. It deliberately resolves no names — turning a code
/// into a song is <see cref="Services.EntitlementResolver"/>'s job, against the
/// catalogue the browser already has.
/// </summary>
public class PlayStationEntitlementsResponse
{
    [JsonPropertyName("generatedAt")]
    public string? GeneratedAt { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("items")]
    public List<PlayStationOwnedItem> Items { get; set; } = [];
}

public class PlayStationOwnedItem
{
    /// <summary>The content code, e.g. <c>RBPHOTOGRCCF04AD</c>.</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    /// <summary>The full entitlement id the code came from.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>"song", "disc" (on-disc bundle) or "bundle" (pack/export).</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
}
