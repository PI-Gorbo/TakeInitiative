using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>
/// <see cref="ArticleMerge"/> (15e.3): an editor's view of the article is merged with the
/// blocks they cannot see, which stay in place. The editor is <see cref="Player"/> unless a
/// test says otherwise; <see cref="Dm"/> writes the secret blocks.
/// </summary>
public class ArticleMergeTests
{
    private static Member NewMember(Role role) => new()
    {
        MemberId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Role = role,
        JoinedAt = DateTimeOffset.UnixEpoch,
    };

    private static readonly Member Dm = NewMember(Role.DM);
    private static readonly Member Player = NewMember(Role.Player);
    private static readonly Member OtherPlayer = NewMember(Role.Player);

    private static readonly Entry Gundren = new()
    {
        Id = Guid.NewGuid(),
        CampaignId = Guid.NewGuid(),
        CreatorMemberId = Player.MemberId,
        Name = "Gundren",
        Visibility = Visibility.Everyone,
    };

    private static ArticleBlock Text(string text, Member? owner = null)
        => new() { Id = Guid.NewGuid(), Text = text, Visibility = Visibility.Everyone, OwnerMemberId = (owner ?? Player).MemberId };

    private static ArticleBlock Secret(string text, Visibility visibility = Visibility.DM, Member? owner = null)
        => new() { Id = Guid.NewGuid(), Text = text, Visibility = visibility, OwnerMemberId = (owner ?? Dm).MemberId };

    private static ArticleBlockEdit Keep(ArticleBlock block) => new(block.Id, block.Text, block.Visibility);

    private static ArticleBlockEdit Change(ArticleBlock block, string text) => new(block.Id, text, block.Visibility);

    private static ArticleBlockEdit Add(string text, Visibility visibility = Visibility.Everyone) => new(null, text, visibility);

    private static IReadOnlyList<ArticleBlock> Merged(IReadOnlyList<ArticleBlock> stored, Member editor, params ArticleBlockEdit[] edits)
    {
        var result = ArticleMerge.Merge(Gundren, stored, edits, editor);
        result.Error.Should().BeNull();
        return result.Blocks;
    }

    private static IEnumerable<string> Texts(IEnumerable<ArticleBlock> blocks) => blocks.Select(b => b.Text);

    [Fact]
    public void AHiddenBlockAtTheStart_StaysFirst()
    {
        var h = Secret("H");
        var a = Text("A");
        var b = Text("B");

        var merged = Merged([h, a, b], Player, Keep(b), Change(a, "A2"));

        Texts(merged).Should().Equal("H", "B", "A2");
    }

    [Fact]
    public void AHiddenBlockInTheMiddle_StaysAfterTheBlockItFollowed()
    {
        var a = Text("A");
        var h = Secret("H");
        var b = Text("B");

        Texts(Merged([a, h, b], Player, Change(a, "A2"), Keep(b), Add("C"))).Should().Equal("A2", "H", "B", "C");
    }

    [Fact]
    public void AHiddenBlockAtTheEnd_StaysLast()
    {
        var a = Text("A");
        var h = Secret("H");

        Texts(Merged([a, h], Player, Keep(a), Add("B"))).Should().Equal("A", "H", "B");
    }

    [Fact]
    public void TwoHiddenBlocksInARow_StayTogetherAndInOrder()
    {
        var a = Text("A");
        var h1 = Secret("H1");
        var h2 = Secret("H2", Visibility.Me);
        var b = Text("B");

        Texts(Merged([a, h1, h2, b], Player, Keep(b), Keep(a))).Should().Equal("B", "A", "H1", "H2");
    }

    [Fact]
    public void AfterADeletedNeighbour_AHiddenBlockGoesAfterTheNearestEarlierBlockThatIsLeft()
    {
        var a = Text("A");
        var b = Text("B");
        var h = Secret("H");
        var c = Text("C");

        Texts(Merged([a, b, h, c], Player, Keep(a), Keep(c))).Should().Equal("A", "H", "C");
    }

    [Fact]
    public void AfterItsOnlyEarlierNeighbourIsDeleted_AHiddenBlockGoesFirst()
    {
        var a = Text("A");
        var h = Secret("H");
        var b = Text("B");

        Texts(Merged([a, h, b], Player, Keep(b))).Should().Equal("H", "B");
    }

    [Fact]
    public void AfterAReorder_AHiddenBlockFollowsItsNeighbourToItsNewPlace()
    {
        var a = Text("A");
        var h = Secret("H");
        var b = Text("B");
        var c = Text("C");

        Texts(Merged([a, h, b, c], Player, Keep(c), Keep(b), Keep(a))).Should().Equal("C", "B", "A", "H");
    }

    [Fact]
    public void ADmSeesEveryBlock_SoNothingIsReinserted()
    {
        var a = Text("A");
        var h = Secret("H");

        Texts(Merged([a, h], Dm, Keep(h))).Should().Equal("H");
    }

    [Fact]
    public void NewBlocks_AreOwnedByTheEditor_AndKeepTheirVisibility()
    {
        var merged = Merged([], Player, Add("Mine", Visibility.Me), Add("Everyone's"));

        merged.Should().AllSatisfy(b => b.OwnerMemberId.Should().Be(Player.MemberId));
        merged.Select(b => b.Visibility).Should().Equal(Visibility.Me, Visibility.Everyone);
        merged.Select(b => b.Id).Should().OnlyHaveUniqueItems().And.NotContain(Guid.Empty);
    }

    [Fact]
    public void AnUnknownId_IsRefused()
    {
        var result = ArticleMerge.Merge(Gundren, [Text("A")], [new ArticleBlockEdit(Guid.NewGuid(), "X", Visibility.Everyone)], Player);

        result.Error.Should().Be(ArticleMergeError.UnknownBlock);
    }

    [Fact]
    public void AHiddenId_IsRefusedExactlyLikeAnUnknownOne()
    {
        var h = Secret("H");
        var result = ArticleMerge.Merge(Gundren, [h], [Change(h, "I saw it")], Player);

        result.Error.Should().Be(ArticleMergeError.UnknownBlock);
        result.BlockId.Should().Be(h.Id);
    }

    [Fact]
    public void TheSameIdTwice_IsRefused()
    {
        var a = Text("A");
        ArticleMerge.Merge(Gundren, [a], [Keep(a), Keep(a)], Player).Error.Should().Be(ArticleMergeError.DuplicateBlock);
    }

    [Fact]
    public void ItsOwner_ChangesABlocksVisibility()
    {
        var mine = Text("A", Player);

        Merged([mine], Player, new ArticleBlockEdit(mine.Id, "A", Visibility.DM)).Single().Visibility.Should().Be(Visibility.DM);
    }

    [Fact]
    public void ADm_ChangesAnyBlocksVisibility_AndTheOwnerStays()
    {
        var players = Secret("P", Visibility.DM, Player);

        var block = Merged([players], Dm, new ArticleBlockEdit(players.Id, "P", Visibility.Everyone)).Single();

        block.Visibility.Should().Be(Visibility.Everyone);
        block.OwnerMemberId.Should().Be(Player.MemberId);
    }

    [Fact]
    public void SomeoneElse_CannotChangeABlocksVisibility_ButCanChangeItsText()
    {
        var players = Text("P", Player);

        ArticleMerge.Merge(Gundren, [players], [new ArticleBlockEdit(players.Id, "P", Visibility.Me)], OtherPlayer)
            .Error.Should().Be(ArticleMergeError.VisibilityNotAllowed);
        Merged([players], OtherPlayer, Change(players, "P2")).Single().Text.Should().Be("P2");
    }

    [Fact]
    public void BlankBlocks_AreRemoved_AndTextIsTrimmed()
    {
        var a = Text("A");
        var h = Secret("H");

        var merged = Merged([a, h], Player, Change(a, "   \n "), Add("  "), Add("\n B \n"));

        Texts(merged).Should().Equal("H", "B");
    }

    [Fact]
    public void AQuotesSource_IsKept()
    {
        var quote = Text("We met Gundren") with
        {
            Quote = new ArticleQuote { NoteId = Guid.NewGuid(), SessionNumber = 12, AuthorMemberId = Player.MemberId },
        };

        Merged([quote], OtherPlayer, Change(quote, "Gundren")).Single().Quote.Should().Be(quote.Quote);
    }

    [Fact]
    public void AnUnchangedView_MergesToTheSameBlocks()
    {
        var a = Text("A");
        var h = Secret("H");
        IReadOnlyList<ArticleBlock> stored = [a, h];

        ArticleMerge.SameBlocks(stored, Merged(stored, Player, Keep(a))).Should().BeTrue();
    }

    public static TheoryData<Visibility, Visibility[], Visibility> NewEntryCases => new()
    {
        { Visibility.Everyone, [Visibility.Everyone], Visibility.Everyone },
        { Visibility.DM, [Visibility.Everyone], Visibility.DM },
        { Visibility.Everyone, [Visibility.DM], Visibility.DM },
        { Visibility.Everyone, [Visibility.Me], Visibility.Me },
        { Visibility.Me, [Visibility.Everyone], Visibility.Me },
        { Visibility.DM, [Visibility.Me], Visibility.Me },
        { Visibility.Everyone, [Visibility.Everyone, Visibility.DM], Visibility.DM },
        { Visibility.Everyone, [Visibility.DM, Visibility.Me], Visibility.Me },
    };

    [Theory]
    [MemberData(nameof(NewEntryCases))]
    public void ANewEntryFromAnArticle_GetsTheNarrowestOfTheEntryAndItsBlocks(Visibility entry, Visibility[] blocks, Visibility expected)
    {
        var id = Guid.NewGuid();
        var mentioning = blocks.Select(v => Secret($"@[New](entry:{id})", v)).ToList();

        PutEntryArticle.NewEntryVisibility(Gundren with { Visibility = entry }, [Text("unrelated") with { Visibility = Visibility.Me }, .. mentioning], id)
            .Should().Be(expected);
    }
}
