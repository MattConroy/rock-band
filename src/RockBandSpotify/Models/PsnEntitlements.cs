using System.Text.Json.Serialization;

namespace RockBandSpotify.Models;

/// <summary>The gateway's response shape (see gateway/worker.js) — raw owned
/// content codes, never resolved names.</summary>
public class PsnEntitlementsResponse
{
    [JsonPropertyName("generatedAt")]
    public string? GeneratedAt { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("items")]
    public List<PsnOwnedItem> Items { get; set; } = new();
}

public class PsnOwnedItem
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>"song" (individual), "disc" (on-disc), or "bundle" (pack/export).</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
}

/// <summary>The static, maintainer-generated entitlement-code database
/// (wwwroot/data/entitlement-codes.json — see tools/generate-entitlement-codes.mjs).
/// Only ever contains fragments actually observed in a real entitlement dump.</summary>
public class EntitlementCodeFile
{
    [JsonPropertyName("codes")]
    public Dictionary<string, int> Codes { get; set; } = new();
}
