using RockBandSpotify.Models;

namespace RockBandSpotify.UnitTests;

/// <summary>
/// Behaviour only. Asserting that "RB1" maps to "Rock Band 1" would just restate
/// the lookup table, so the tests here cover what the lookup <i>does</i> when a
/// code is missing or a song carries several.
/// </summary>
public class SourceCatalogTests
{
    [Fact]
    public void Name_falls_back_to_the_raw_code_when_it_is_not_in_the_table()
    {
        // Guards the case that matters: catalogue.json gains a source before the
        // table knows about it. The column should degrade to the code, not blank.
        Assert.Equal("MYSTERY", SourceCatalog.Name("MYSTERY"));
    }

    [Fact]
    public void Name_returns_empty_for_null()
        => Assert.Equal("", SourceCatalog.Name(null));

    [Fact]
    public void Names_joins_every_source_a_song_shipped_in()
        => Assert.Equal("Rock Band 2 · Rock Band Unplugged", SourceCatalog.Names(["RB2", "UNPLUGGED"]));

    [Fact]
    public void Names_of_a_single_source_is_just_that_name()
        => Assert.Equal("Rock Band 1", SourceCatalog.Names(["RB1"]));

    [Fact]
    public void Names_of_nothing_is_empty_rather_than_null()
        => Assert.Equal("", SourceCatalog.Names([]));

    [Fact]
    public void Names_applies_the_raw_code_fallback_per_entry()
        => Assert.Equal("Rock Band 2 · MYSTERY", SourceCatalog.Names(["RB2", "MYSTERY"]));
}
