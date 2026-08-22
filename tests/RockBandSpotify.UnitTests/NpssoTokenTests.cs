using RockBandSpotify.Services;

namespace RockBandSpotify.UnitTests;

/// <summary>
/// Sony shows the token as a JSON line, and copying the whole line is easier
/// than selecting the value inside the quotes, so the paste arrives in more
/// than one shape.
/// </summary>
public class NpssoTokenTests
{
    private const string Token = "AbCd1234567890TOKENvalue";

    [Fact]
    public void The_bare_value_is_taken_as_is()
        => Assert.Equal(Token, NpssoToken.Extract(Token));

    [Fact]
    public void The_whole_json_line_is_unwrapped()
        => Assert.Equal(Token, NpssoToken.Extract($"{{\"npsso\":\"{Token}\"}}"));

    [Fact]
    public void Spacing_inside_the_json_does_not_matter()
        => Assert.Equal(Token, NpssoToken.Extract($"{{ \"npsso\" : \"{Token}\" }}"));

    [Fact]
    public void The_json_is_found_inside_a_larger_paste()
    {
        // Selecting the whole page picks up the surrounding text too.
        var page = $"Some text\n{{\"npsso\":\"{Token}\"}}\nmore text";
        Assert.Equal(Token, NpssoToken.Extract(page));
    }

    [Fact]
    public void The_cookie_form_is_accepted()
        => Assert.Equal(Token, NpssoToken.Extract($"npsso={Token}; path=/"));

    [Fact]
    public void Surrounding_quotes_and_whitespace_are_stripped()
        => Assert.Equal(Token, NpssoToken.Extract($"  \"{Token}\"  "));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"\"")]
    public void Nothing_usable_gives_null(string? pasted)
        => Assert.Null(NpssoToken.Extract(pasted));

    [Fact]
    public void A_half_copied_paste_is_rejected_rather_than_sent()
    {
        // Leftover punctuation means the copy went wrong. Sending it earns a
        // 401 that reads as though the account is at fault.
        Assert.Null(NpssoToken.Extract("{\"npsso\":"));
        Assert.Null(NpssoToken.Extract("npsso value goes here"));
    }

    [Fact]
    public void A_json_value_wins_over_the_text_around_it()
    {
        // The key name appears in the paste twice; the quoted value is the one
        // that matters.
        Assert.Equal(Token, NpssoToken.Extract($"npsso is: {{\"npsso\":\"{Token}\"}}"));
    }
}
