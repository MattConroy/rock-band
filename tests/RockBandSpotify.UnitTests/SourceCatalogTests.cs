using RockBandSpotify.Models;

namespace RockBandSpotify.UnitTests;

public class SourceCatalogTests
{
    [Theory]
    [InlineData("RB1", "Rock Band 1")]
    [InlineData("RB4_DLC", "Rock Band 4 DLC")]
    [InlineData("RBN2", "Rock Band Network 2")]
    [InlineData("TBRB", "The Beatles: Rock Band")]
    [InlineData("RIVALS", "Rock Band Rivals")]
    public void Name_returns_full_name_for_known_codes(string code, string expected)
        => Assert.Equal(expected, SourceCatalog.Name(code));

    [Fact]
    public void Name_falls_back_to_raw_code_for_unknown_values()
        => Assert.Equal("MYSTERY", SourceCatalog.Name("MYSTERY"));

    [Fact]
    public void Name_returns_empty_for_null()
        => Assert.Equal("", SourceCatalog.Name(null));

    [Fact]
    public void Description_is_present_for_every_known_code()
        => Assert.NotNull(SourceCatalog.Description("RB1"));

    [Fact]
    public void Description_is_null_for_unknown_codes()
        => Assert.Null(SourceCatalog.Description("MYSTERY"));
}
