using System.Text.RegularExpressions;

namespace RockBandSpotify.Services;

/// <summary>
/// The 7-character fragment PSN embeds in a Rock Band song's entitlement code
/// (e.g. "Believer" -&gt; "BELIEVE", "21 Guns" -&gt; "21GUNSX") — reverse-engineered
/// from a real account's owned-entitlement codes: strip everything but letters
/// and digits, uppercase, then truncate or right-pad with 'X' to exactly 7
/// characters. Deterministic and needs no external data, but it's a lossy hash:
/// many different titles collapse to the same fragment (~24% of the catalogue),
/// so a fragment match is only trustworthy when it's unique across the catalogue.
/// </summary>
public static class EntitlementCode
{
    private static readonly Regex NonAlphaNumeric = new("[^A-Za-z0-9]", RegexOptions.Compiled);

    public static string Fragment(string title)
    {
        var cleaned = NonAlphaNumeric.Replace(title, "").ToUpperInvariant();
        return cleaned.Length >= 7 ? cleaned[..7] : cleaned.PadRight(7, 'X');
    }

    /// <summary>
    /// Extracts the 7-character fragment from a real PSN content code (e.g.
    /// "RBBELIEVERXX2775" or "XRBBELIEVERXX2775" -&gt; "BELIEVE"). Returns null for
    /// codes that don't fit the standard RB/XRB-prefix-plus-7-char shape (a small
    /// number of legacy/placeholder codes don't).
    /// </summary>
    public static string? FromOwnedCode(string code)
    {
        string? rest = code.StartsWith("XRB", StringComparison.Ordinal) ? code[3..]
            : code.StartsWith("RB", StringComparison.Ordinal) ? code[2..]
            : null;
        return rest is { Length: >= 7 } ? rest[..7] : null;
    }
}
