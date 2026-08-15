namespace RockBandSpotify.Models;

/// <summary>
/// Full names and explanations for the catalogue's Origin/era codes (RB1, DLC3,
/// RBN2, ...). Unrecognized codes fall back to the raw code with no description.
/// </summary>
public static class EraCatalog
{
    private static readonly Dictionary<string, (string Name, string Description)> Info = new()
    {
        ["RB1"] = ("Rock Band 1", "On-disc tracklist of the original Rock Band (2007)."),
        ["RB2"] = ("Rock Band 2", "On-disc tracklist of Rock Band 2 (2008)."),
        ["RB3"] = ("Rock Band 3", "On-disc tracklist of Rock Band 3 (2010)."),
        ["RB4"] = ("Rock Band 4", "On-disc tracklist of Rock Band 4 (2015)."),
        ["DLC1/2"] = ("Rock Band 1 & 2 DLC", "Downloadable songs released during the Rock Band 1/2 era (2007–2009)."),
        ["DLC3"] = ("Rock Band 3 DLC", "Downloadable songs released during the Rock Band 3 era (2010–2013)."),
        ["DLC4"] = ("Rock Band 4 DLC", "Downloadable songs released for Rock Band 4 (2015 onward)."),
        ["RBN1"] = ("Rock Band Network 1", "Community-authored songs, officially licensed and sold (2010–2013)."),
        ["RBN2"] = ("Rock Band Network 2", "Community-authored songs from the relaunched Network (2015–2016)."),
        ["LEGO"] = ("LEGO Rock Band", "On-disc tracklist of the LEGO-themed spin-off (2009)."),
        ["GDRB"] = ("Green Day: Rock Band", "On-disc tracklist of the Green Day-themed spin-off (2010)."),
        ["TBRB"] = ("The Beatles: Rock Band", "On-disc tracklist of the Beatles-themed spin-off (2009)."),
        ["TBRB DLC"] = ("The Beatles: Rock Band DLC", "Downloadable songs for the Beatles-themed spin-off."),
        ["RBVR"] = ("Rock Band VR", "Tracklist exclusive to the VR spin-off (2016)."),
        ["CTP2"] = ("Country Track Pack 2", "A themed multi-song pack of country tracks."),
        ["AC/DC TP"] = ("AC/DC Track Pack", "A themed multi-song pack of AC/DC tracks."),
    };

    public static string Name(string? code) =>
        code is not null && Info.TryGetValue(code, out var info) ? info.Name : code ?? "";

    public static string? Description(string? code) =>
        code is not null && Info.TryGetValue(code, out var info) ? info.Description : null;
}
