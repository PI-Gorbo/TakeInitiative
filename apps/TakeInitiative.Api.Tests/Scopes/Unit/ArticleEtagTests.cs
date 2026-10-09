using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>
/// <see cref="ArticleEtag"/> and <see cref="ArticleHistory"/> (15e.4): both are per viewer, so a
/// change to a block a viewer cannot see leaves no trace for them. The viewers are a DM, the
/// block's owner (a player) and another player.
/// </summary>
public class ArticleEtagTests
{
    private static Member NewMember(Role role) => new()
    {
        MemberId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Role = role,
        JoinedAt = DateTimeOffset.UnixEpoch,
    };

    private static readonly Member Dm = NewMember(Role.DM);
    private static readonly Member Owner = NewMember(Role.Player);
    private static readonly Member Other = NewMember(Role.Player);

    private static readonly ArticleBlock Ordinary = new() { Id = Guid.NewGuid(), Text = "Dwarf prospector.", Visibility = Visibility.Everyone, OwnerMemberId = Owner.MemberId };
    private static readonly ArticleBlock DmSecret = new() { Id = Guid.NewGuid(), Text = "Captured by Klarg.", Visibility = Visibility.DM, OwnerMemberId = Owner.MemberId };
    private static readonly ArticleBlock DmOwnSecret = new() { Id = Guid.NewGuid(), Text = "Glasstaff is Iarno.", Visibility = Visibility.DM, OwnerMemberId = Dm.MemberId };
    private static readonly ArticleBlock OwnersNote = new() { Id = Guid.NewGuid(), Text = "I owe him gold.", Visibility = Visibility.Me, OwnerMemberId = Owner.MemberId };

    private static Entry WithBlocks(params ArticleBlock[] blocks) => new()
    {
        Id = Guid.NewGuid(),
        CampaignId = Guid.NewGuid(),
        CreatorMemberId = Owner.MemberId,
        Name = "Gundren",
        Visibility = Visibility.Everyone,
        Article = new Article { Blocks = blocks },
    };

    private static readonly Entry Before = WithBlocks(Ordinary, DmSecret, DmOwnSecret, OwnersNote);

    private static string Etag(Entry entry, Member viewer) => ArticleEtag.For(entry, viewer);

    [Fact]
    public void AChangeToAHiddenBlock_LeavesTheViewersEtagAlone()
    {
        var after = WithBlocks(Ordinary, DmSecret, DmOwnSecret with { Text = "Glasstaff is Iarno Albrek." }, OwnersNote) with { Id = Before.Id };

        Etag(after, Other).Should().Be(Etag(Before, Other));
        Etag(after, Owner).Should().Be(Etag(Before, Owner));
        Etag(after, Dm).Should().NotBe(Etag(Before, Dm));
    }

    [Fact]
    public void AMeBlock_ChangesOnlyItsOwnersEtag()
    {
        var after = WithBlocks(Ordinary, DmSecret, DmOwnSecret, OwnersNote with { Text = "I owe him 10 gold." });

        Etag(after, Owner).Should().NotBe(Etag(Before, Owner));
        Etag(after, Dm).Should().Be(Etag(Before, Dm));
        Etag(after, Other).Should().Be(Etag(Before, Other));
    }

    [Fact]
    public void AddingOrRemovingAHiddenBlock_LeavesTheViewersEtagAlone()
    {
        var removed = WithBlocks(Ordinary, DmSecret, OwnersNote);

        Etag(removed, Other).Should().Be(Etag(Before, Other));
        Etag(removed, Owner).Should().Be(Etag(Before, Owner));
    }

    [Fact]
    public void AChangeToAVisibleBlock_ChangesEveryonesEtag()
    {
        var after = WithBlocks(Ordinary with { Text = "Dwarf." }, DmSecret, DmOwnSecret, OwnersNote);

        foreach (var viewer in new[] { Dm, Owner, Other })
        {
            Etag(after, viewer).Should().NotBe(Etag(Before, viewer));
        }
    }

    [Fact]
    public void TheEtagCoversOrderAndVisibility()
    {
        var reordered = WithBlocks(DmSecret, Ordinary, DmOwnSecret, OwnersNote);
        var revealed = WithBlocks(Ordinary, DmSecret with { Visibility = Visibility.Everyone }, DmOwnSecret, OwnersNote);

        Etag(reordered, Dm).Should().NotBe(Etag(Before, Dm));
        Etag(revealed, Owner).Should().NotBe(Etag(Before, Owner), "the owner saw the block before, and its visibility changed");
        Etag(revealed, Other).Should().NotBe(Etag(Before, Other), "the other player now sees the block");
    }

    [Fact]
    public void TwoViewersWithTheSameView_HaveTheSameEtag()
    {
        var everyone = WithBlocks(Ordinary);
        Etag(everyone, Dm).Should().Be(Etag(everyone, Other));
    }

    [Fact]
    public void AnEntryTheViewerCannotSee_HasAnEmptyView()
    {
        var dmEntry = Before with { Visibility = Visibility.DM };
        ArticleView.VisibleBlocks(dmEntry, Other).Should().BeEmpty();
        ArticleView.VisibleBlocks(dmEntry, Dm).Should().HaveCount(3);
    }

    [Fact]
    public void History_ListsOnlyVisibleBlocks_AndLeavesOutVersionsThatChangedNothingTheViewerSees()
    {
        var actor = Actor.Member(Dm.MemberId);
        var t0 = DateTimeOffset.UnixEpoch;
        ArticleVersion[] versions =
        [
            new(t0, actor, [Ordinary]),
            new(t0.AddMinutes(1), actor, [Ordinary, DmOwnSecret]),
            new(t0.AddMinutes(2), actor, [Ordinary, DmOwnSecret with { Text = "Glasstaff is Iarno Albrek." }]),
            new(t0.AddMinutes(3), actor, [Ordinary with { Text = "Dwarf." }, DmOwnSecret]),
        ];

        var forOther = ArticleHistory.For(Before, versions, Other);
        var forDm = ArticleHistory.For(Before, versions, Dm);

        forOther.Select(v => v.At).Should().Equal(t0, t0.AddMinutes(3));
        forOther.SelectMany(v => v.Blocks).Should().NotContain(b => b.Id == DmOwnSecret.Id);
        forDm.Should().HaveCount(4);
    }
}
