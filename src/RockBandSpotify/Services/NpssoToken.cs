using System.Text.RegularExpressions;

namespace RockBandSpotify.Services;

/// <summary>Whether a paste can be used, and if not, why not.</summary>
public enum NpssoState
{
    /// <summary>Nothing pasted yet — not a failure, just no answer to give.</summary>
    Empty,

    /// <summary>A token was found and looks like one.</summary>
    Valid,

    /// <summary>Something was pasted, but it can't be a token.</summary>
    Invalid,
}

/// <summary>The verdict on a paste, with a message explaining it.</summary>
/// <param name="State">Whether the paste is usable.</param>
/// <param name="Token">The extracted token, set only when <see cref="State"/> is Valid.</param>
/// <param name="Message">Why, in words meant for the person who pasted it.</param>
public readonly record struct NpssoCheck(NpssoState State, string? Token, string Message)
{
    public bool IsValid => State == NpssoState.Valid;
}

/// <summary>
/// Reads the npsso out of whatever gets pasted into the connect dialog, and
/// says whether it can be used.
///
/// <para>
/// Sony's page shows the token as a JSON line, and copying the whole line is
/// far easier than selecting the value inside the quotes — so that is the form
/// most people arrive with. Some browsers show it as a cookie instead. All
/// three forms are accepted rather than asking anyone to edit their clipboard.
/// </para>
///
/// <para>
/// Checking the shape here is worth the fuss: a truncated copy is otherwise
/// indistinguishable from an expired token once PlayStation rejects it, and
/// "your login failed" sends people back to sign in again when the real
/// problem was the clipboard.
/// </para>
/// </summary>
public static partial class NpssoToken
{
    /// <summary>
    /// Sony issues a 64-character token. Checking the length catches a
    /// half-selected copy, which is the common way this goes wrong. If Sony
    /// ever changes it, the message below names both numbers, so the cause is
    /// visible rather than mysterious.
    /// </summary>
    public const int TokenLength = 64;

    private const string PasteHint =
        "Paste the whole {\"npsso\":\"…\"} line, or just the value between the quotes.";

    /// <summary>Extracts and checks in one go.</summary>
    public static NpssoCheck Check(string? pasted)
    {
        if (string.IsNullOrWhiteSpace(pasted))
            return new(NpssoState.Empty, null, PasteHint);

        var token = Extract(pasted);
        if (token is null)
            return new(NpssoState.Invalid, null, $"That doesn't look like a token. {PasteHint}");

        if (!TokenCharacters().IsMatch(token))
            return new(NpssoState.Invalid, null,
                "That contains characters a token doesn't use — the copy probably picked up something extra.");

        if (token.Length != TokenLength)
            return new(NpssoState.Invalid, null,
                $"A token is {TokenLength} characters; this is {token.Length}. "
                + (token.Length < TokenLength ? "Looks like part of it was left behind." : "Looks like it picked up something extra."));

        return new(NpssoState.Valid, token, $"Looks right — {TokenLength} characters.");
    }

    /// <summary>
    /// The token as it appears in the paste, whatever form it came in, or null
    /// when there is nothing token-shaped to take. Says nothing about whether
    /// the result is usable — see <see cref="Check"/>.
    /// </summary>
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
    /// Rejects a value that still carries whitespace or JSON punctuation —
    /// that is a half-copied paste rather than a token of any length.
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

    /// <summary>The token is URL-safe base64, so this is its whole alphabet.</summary>
    [GeneratedRegex(@"^[A-Za-z0-9_-]+$")]
    private static partial Regex TokenCharacters();
}
