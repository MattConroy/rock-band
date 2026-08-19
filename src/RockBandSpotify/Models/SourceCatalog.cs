namespace RockBandSpotify.Models;

/// <summary>
/// Display names for the codes in <see cref="CatalogueSong.Sources"/> — a
/// mainline game, a full spin-off game, an exclusive pack/expansion, an era DLC
/// bucket, or the Rock Band Network.
/// <para>
/// A code with no entry here renders as the raw code, so a catalogue that gains
/// a new source still shows something rather than a blank cell.
/// </para>
/// </summary>
public static class SourceCatalog
{
    private static readonly Dictionary<string, string> ByCode = new()
    {
        ["RB1"] = "Rock Band 1",
        ["RB2"] = "Rock Band 2",
        ["RB3"] = "Rock Band 3",
        ["RB4"] = "Rock Band 4",
        ["RB1_DLC"] = "Rock Band 1 DLC",
        ["RB2_DLC"] = "Rock Band 2 DLC",
        ["RB3_DLC"] = "Rock Band 3 DLC",
        ["RB4_DLC"] = "Rock Band 4 DLC",
        ["RBN1"] = "Rock Band Network 1",
        ["RBN2"] = "Rock Band Network 2",
        ["LEGO"] = "LEGO Rock Band",
        ["GDRB"] = "Green Day: Rock Band",
        ["TBRB"] = "The Beatles: Rock Band",
        ["RIVALS"] = "Rock Band Rivals",
        ["UNPLUGGED"] = "Rock Band Unplugged",
        ["RELOADED"] = "Rock Band Reloaded",
        ["BLITZ"] = "Rock Band Blitz",
        ["CTP2"] = "Country Track Pack 2",
        ["ACDC_TP"] = "AC/DC Track Pack",
    };

    public static string Name(string? code) =>
        code is not null && ByCode.TryGetValue(code, out var name) ? name : code ?? "";

    /// <summary>Every source a song shipped in, mainline first, as one cell.</summary>
    public static string Names(IReadOnlyList<string> codes) =>
        string.Join(" · ", codes.Select(Name));
}
