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
        // Two disc tracks with no listing of their own: the export grants both,
        // and the second was also sold standalone later.
        Song(4, "Creep", "RBRB1DISCEXP2462"),
        Song(5, "Wanted Dead or Alive", "RBRB1DISCEXP2462", "RBWANTEDDCCF0456"),
        // Rock Band 4 was never exported, so owning the game is the only way to
        // have its tracks and the game's own entitlement is what grants them.
        Song(6, "Uptight", "ROCKBAND4PS4000E"),
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
    public void One_export_code_grants_every_song_on_that_disc()
    {
        // A disc song has no store id of its own — the export's code is what
        // identifies it, so a single code has to yield more than one song.
        var r = Resolve("RBRB1DISCEXP2462");
        Assert.Equal([4, 5], r.Matched.Select(s => s.Id));
        Assert.Empty(r.Unmatched);
    }

    [Fact]
    public void A_song_granted_by_both_an_export_and_its_own_listing_is_listed_once()
    {
        var r = Resolve("RBRB1DISCEXP2462", "RBWANTEDDCCF0456");
        Assert.Equal([4, 5], r.Matched.Select(s => s.Id));
    }

    [Fact]
    public void An_export_and_an_individual_purchase_both_contribute()
    {
        var r = Resolve("RBPHOTOGRCCF04AD", "RBRB1DISCEXP2462");
        Assert.Equal([1, 4, 5], r.Matched.Select(s => s.Id));
    }

    [Fact]
    public void Owning_a_game_grants_the_songs_that_shipped_in_it()
        => Assert.Equal([6], Resolve("ROCKBAND4PS4000E").Matched.Select(s => s.Id));

    [Fact]
    public void Every_code_ends_up_either_matched_or_unmatched()
    {
        var r = Resolve("RBPHOTOGRCCF04AD", "PROCKBAND3000072");
        Assert.Single(r.Matched);
        Assert.Single(r.Unmatched);
    }
}
