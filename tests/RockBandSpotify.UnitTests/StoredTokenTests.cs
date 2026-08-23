using RockBandSpotify.Models;

namespace RockBandSpotify.UnitTests;

/// <summary>
/// A token granted before a scope was added still works for some calls and
/// fails others with a bare 403, which reads as a permission problem nobody
/// can act on. The stored scope is what lets that be caught before the call.
/// </summary>
public class StoredTokenTests
{
    private static StoredToken Token(string? scope) => new() { AccessToken = "x", Scope = scope };

    [Fact]
    public void A_token_covers_the_scopes_it_was_granted()
        => Assert.True(Token("playlist-read-private playlist-modify-private")
            .Covers("playlist-read-private playlist-modify-private"));

    [Fact]
    public void Order_does_not_matter()
        => Assert.True(Token("b a").Covers("a b"));

    [Fact]
    public void Extra_granted_scopes_are_fine()
        => Assert.True(Token("a b c").Covers("a b"));

    [Fact]
    public void A_missing_scope_is_not_covered()
    {
        // Exactly the case that produced the 403: reading the playlist list
        // needs playlist-read-private, which the app didn't ask for.
        var token = Token("playlist-modify-public playlist-modify-private");

        Assert.False(token.Covers("playlist-read-private playlist-modify-public playlist-modify-private"));
    }

    [Fact]
    public void A_token_stored_before_scopes_were_recorded_covers_nothing()
    {
        // Tokens saved by an earlier build have no scope at all. Treating that
        // as "covers everything" would leave the 403 in place for anyone who
        // had already signed in.
        Assert.False(Token(null).Covers("playlist-read-private"));
    }

    [Fact]
    public void Requiring_nothing_is_always_covered()
        => Assert.True(Token(null).Covers(""));
}
