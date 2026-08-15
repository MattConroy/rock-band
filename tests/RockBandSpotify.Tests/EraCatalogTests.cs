using RockBandSpotify.Models;

namespace RockBandSpotify.Tests;

public class EraCatalogTests
{
    [Theory]
    [InlineData("RB1", "Rock Band 1")]
    [InlineData("DLC4", "Rock Band 4 DLC")]
    [InlineData("RBN2", "Rock Band Network 2")]
    [InlineData("TBRB DLC", "The Beatles: Rock Band DLC")]
    public void Name_returns_full_name_for_known_codes(string code, string expected)
        => Assert.Equal(expected, EraCatalog.Name(code));

    [Fact]
    public void Name_falls_back_to_raw_code_for_unknown_values()
        => Assert.Equal("MYSTERY", EraCatalog.Name("MYSTERY"));

    [Fact]
    public void Name_returns_empty_for_null()
        => Assert.Equal("", EraCatalog.Name(null));

    [Fact]
    public void Description_is_present_for_every_known_code()
        => Assert.NotNull(EraCatalog.Description("RB1"));

    [Fact]
    public void Description_is_null_for_unknown_codes()
        => Assert.Null(EraCatalog.Description("MYSTERY"));
}
