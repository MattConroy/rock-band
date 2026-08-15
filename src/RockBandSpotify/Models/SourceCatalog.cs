namespace RockBandSpotify.Models;

/// <summary>
/// Full names and explanations for the catalogue's Source codes — how a song
/// was actually obtainable: an on-disc mainline game, a full spin-off game, an
/// exclusive pack/expansion, era-specific DLC, or the Rock Band Network.
/// Unrecognized codes fall back to the raw code with no description.
/// </summary>
public static class SourceCatalog
{
    private static readonly Dictionary<string, (string Name, string Description)> Info = new()
    {
        ["RB1"] = ("Rock Band 1", "On-disc tracklist of the original Rock Band (2007)."),
        ["RB2"] = ("Rock Band 2", "On-disc tracklist of Rock Band 2 (2008)."),
        ["RB3"] = ("Rock Band 3", "On-disc tracklist of Rock Band 3 (2010)."),
        ["RB4"] = ("Rock Band 4", "On-disc tracklist of Rock Band 4 (2015)."),
        ["RB1_DLC"] = ("Rock Band 1 DLC", "Weekly DLC released before Rock Band 2 existed (Nov 2007 – Sep 2008)."),
        ["RB2_DLC"] = ("Rock Band 2 DLC", "Weekly DLC released during the Rock Band 2 era (Sep 2008 – Oct 2010)."),
        ["RB3_DLC"] = ("Rock Band 3 DLC", "Weekly DLC released during the Rock Band 3 era (Oct 2010 – Oct 2015)."),
        ["RB4_DLC"] = ("Rock Band 4 DLC", "Weekly DLC released for Rock Band 4 (Oct 2015 onward)."),
        ["RBN1"] = ("Rock Band Network 1", "Community-authored songs, officially licensed and sold (2010–2011)."),
        ["RBN2"] = ("Rock Band Network 2", "Community-authored songs from the relaunched Network (2011–2014)."),
        ["LEGO"] = ("LEGO Rock Band", "On-disc tracklist of the LEGO-themed spin-off (2009)."),
        ["GDRB"] = ("Green Day: Rock Band", "On-disc tracklist of the Green Day-themed spin-off (2010)."),
        ["TBRB"] = ("The Beatles: Rock Band", "Disc and DLC for the Beatles-themed spin-off — never cross-compatible with the mainline games."),
        ["RIVALS"] = ("Rock Band Rivals", "Songs exclusive to the Rock Band 4 Rivals update (2017) — not purchasable as regular DLC."),
        ["UNPLUGGED"] = ("Rock Band Unplugged", "Songs that originated on Rock Band Unplugged (PSP, 2009)."),
        ["RELOADED"] = ("Rock Band Reloaded", "Songs that originated on Rock Band Reloaded (mobile)."),
        ["BLITZ"] = ("Rock Band Blitz", "Songs that originated on Rock Band Blitz (2012)."),
        ["CTP2"] = ("Country Track Pack 2", "Exclusive to the pack when it released, not sold as individual DLC."),
        ["ACDC_TP"] = ("AC/DC Track Pack", "Never sold as individual DLC — only obtainable by owning the pack."),
    };

    public static string Name(string? code) =>
        code is not null && Info.TryGetValue(code, out var info) ? info.Name : code ?? "";

    public static string? Description(string? code) =>
        code is not null && Info.TryGetValue(code, out var info) ? info.Description : null;
}
