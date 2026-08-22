using System.Text.RegularExpressions;

namespace RockBandSpotify.Services;

/// <summary>
/// Pulls the npsso out of whatever gets pasted into the connect dialog.
///
/// <para>
/// Sony's page shows the token as a JSON line, and copying the whole line is
/// far easier than selecting the value inside the quotes — so that is the form
/// most people arrive with. Some browsers show it as a cookie instead. All
/// three forms are accepted rather than asking anyone to edit their clipboard.
/// </para>
/// </summary>
public static partial class NpssoToken
{
    /// <summary>The token, or null when nothing usable was pasted.</summary>
    public static string? Extract(string? pasted)
    {
        if (string.IsNullOrWhiteSpace(pasted)) return null;
        var text = pasted.Trim();

        // {"npsso":"…"} — the whole JSON line, or a larger blob containing it.
        var json = JsonForm().Match(text);
        if (json.Success) return Clean(json.Groups[1].Value);

        // npsso=… — the cookie form.
        var cookie = CookieForm().Match(text);
        if (cookie.Success) return Clean(cookie.Groups[1].Value);

        // Otherwise the bare value, possibly still wrapped in quotes.
        return Clean(text);
    }

    /// <summary>
    /// Rejects a value that still carries whitespace or JSON punctuation. That
    /// is a half-copied paste rather than a token, and sending it only earns a
    /// 401 that reads as though the account is the problem.
    /// </summary>
    private static string? Clean(string value)
    {
        var token = value.Trim().Trim('"', '\'').Trim();
        if (token.Length == 0) return null;
        if (token.Any(char.IsWhiteSpace)) return null;
        if (token.Contains('{') || token.Contains('}')) return null;
        return token;
    }

    [GeneratedRegex("\"npsso\"\\s*:\\s*\"([^\"]+)\"")]
    private static partial Regex JsonForm();

    [GeneratedRegex(@"\bnpsso=([^;\s""]+)")]
    private static partial Regex CookieForm();
}
