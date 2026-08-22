using RockBandSpotify.Services;

namespace RockBandSpotify.UnitTests;

/// <summary>
/// Sony shows the token as a JSON line, and copying the whole line is easier
/// than selecting the value inside the quotes, so the paste arrives in more
/// than one shape.
/// </summary>
public class NpssoTokenTests
{
    /// <summary>A token of the length Sony actually issues.</summary>
    private const string Token = "AbCd1234567890TOKENvalueQwErTyUiOpAsDfGhJkLzXcVbNm0987654321_x-Y";

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
    public void A_token_of_the_right_shape_is_valid()
    {
        var check = NpssoToken.Check($"{{\"npsso\":\"{Token}\"}}");

        Assert.Equal(NpssoState.Valid, check.State);
        Assert.Equal(Token, check.Token);
        Assert.Contains("64", check.Message);
    }

    [Fact]
    public void Nothing_pasted_yet_is_neither_valid_nor_wrong()
    {
        // The field starts empty; scolding someone before they have typed is
        // noise, so this is its own state rather than a failure.
        var check = NpssoToken.Check("");

        Assert.Equal(NpssoState.Empty, check.State);
        Assert.Null(check.Token);
    }

    [Fact]
    public void A_truncated_copy_says_so_rather_than_being_sent()
    {
        // The common failure: half the token selected. Sending it earns a 401
        // that reads as an expired login, sending people back to sign in again
        // when the clipboard was the problem.
        var check = NpssoToken.Check(Token[..40]);

        Assert.Equal(NpssoState.Invalid, check.State);
        Assert.Null(check.Token);
        Assert.Contains("40", check.Message);
        Assert.Contains("64", check.Message);
    }

    [Fact]
    public void Something_longer_than_a_token_is_rejected_too()
    {
        var check = NpssoToken.Check(Token + "extra");

        Assert.Equal(NpssoState.Invalid, check.State);
        Assert.Contains("69", check.Message);
    }

    [Fact]
    public void Characters_a_token_never_contains_are_rejected()
    {
        var check = NpssoToken.Check(new string('!', NpssoToken.TokenLength));

        Assert.Equal(NpssoState.Invalid, check.State);
        Assert.Contains("characters", check.Message);
    }

    [Fact]
    public void The_full_alphabet_a_token_uses_is_accepted()
    {
        // URL-safe base64, so the two symbols matter as much as the letters.
        var token = ("aZ09_-" + new string('x', NpssoToken.TokenLength - 6));

        Assert.Equal(NpssoState.Valid, NpssoToken.Check(token).State);
    }

    [Fact]
    public void An_unusable_paste_never_yields_a_token_to_send()
    {
        foreach (var pasted in new[] { "", "   ", "short", "npsso value goes here", new string('!', 64) })
            Assert.Null(NpssoToken.Check(pasted).Token);
    }

    [Fact]
    public void A_json_value_wins_over_the_text_around_it()
    {
        // The key name appears in the paste twice; the quoted value is the one
        // that matters.
        Assert.Equal(Token, NpssoToken.Extract($"npsso is: {{\"npsso\":\"{Token}\"}}"));
    }
}
