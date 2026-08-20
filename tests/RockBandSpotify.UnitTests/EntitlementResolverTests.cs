using RockBandSpotify.Models;
using RockBandSpotify.Services;

namespace RockBandSpotify.UnitTests;

public class EntitlementResolverTests
{
    private static CatalogueSong Song(int id, string title, params string[] psnIds)
        => new() { Id = id, Song = title, Artist = "A", PsnIds = [.. psnIds] };

    private static readonly List<CatalogueSong> Catalogue =
    [
        Song(1, "Photograph", "RBPHOTOGRCCF04AD"),
        Song(2, "Everlong", "RBEVERLONCCF0123", "RBFOOPACKCCF0001"), // sold alone and in a pack
        Song(3, "Call Me"),                                          // delisted: no store id
    ];

    private static OwnedSongs Resolve(params string[] codes)
        => EntitlementResolver.Resolve(codes, Catalogue);

    [Fact]
    public void A_code_in_the_catalogue_identifies_its_song()
        => Assert.Equal([1], Resolve("RBPHOTOGRCCF04AD").Matched.Select(s => s.Id));

    [Fact]
    public void A_code_not_in_the_catalogue_is_reported_unmatched()
    {
        var r = Resolve("PROCKBAND3000072");
        Assert.Empty(r.Matched);
        Assert.Equal(["PROCKBAND3000072"], r.Unmatched);
    }

    [Fact]
    public void Comparison_ignores_case()
        => Assert.Single(Resolve("rbphotogrccf04ad").Matched);

    [Fact]
    public void Owning_a_song_twice_lists_it_once()
    {
        // Both the standalone purchase and the pack name the same song.
        var r = Resolve("RBEVERLONCCF0123", "RBFOOPACKCCF0001");
        Assert.Equal([2], r.Matched.Select(s => s.Id));
    }

    [Fact]
    public void A_repeated_code_is_only_counted_once()
        => Assert.Single(Resolve("RBPHOTOGRCCF04AD", "RBPHOTOGRCCF04AD").Matched);

    [Fact]
    public void A_song_with_no_store_id_is_never_matched()
    {
        // "Call Me" is delisted and has no id, so no code can name it.
        var r = Resolve("RBCALLMEXCCF0001");
        Assert.Empty(r.Matched);
    }

    [Fact]
    public void Blank_codes_are_ignored_entirely()
    {
        var r = Resolve("", "   ");
        Assert.Empty(r.Matched);
        Assert.Empty(r.Unmatched);
    }

    [Fact]
    public void Every_code_ends_up_either_matched_or_unmatched()
    {
        var r = Resolve("RBPHOTOGRCCF04AD", "PROCKBAND3000072");
        Assert.Single(r.Matched);
        Assert.Single(r.Unmatched);
    }
}
