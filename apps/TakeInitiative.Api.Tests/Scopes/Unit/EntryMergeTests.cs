using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>
/// The pure merge rules (15g.1): the visibility guard over every pair of entry visibilities and
/// creators, the claim rule, and when stats move. The guard must agree with the read rule: a
/// merge is allowed exactly when nobody who sees the target is outside the merged entry.
/// </summary>
public class EntryMergeTests
{
    private static readonly Member Dm = Viewer(Role.DM);
    private static readonly Member OtherDm = Viewer(Role.DM);
    private static readonly Member PlayerA = Viewer(Role.Player);
    private static readonly Member PlayerB = Viewer(Role.Player);
    private static readonly Member[] Members = [Dm, OtherDm, PlayerA, PlayerB];

    private static Member Viewer(Role role) => new()
    {
        MemberId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Role = role,
        JoinedAt = DateTimeOffset.UnixEpoch,
    };

    private static Entry AnEntry(Visibility visibility, Member creator, EntryKind kind = EntryKind.Character) => new()
    {
        Id = Guid.NewGuid(),
        CampaignId = Guid.Empty,
        CreatorMemberId = creator.MemberId,
        Name = "X",
        Kind = kind,
        Visibility = visibility,
    };

    public static IEnumerable<object[]> Pairs()
    {
        var visibilities = new[] { Visibility.Everyone, Visibility.DM, Visibility.Me };
        var creators = new[] { 0, 2, 3 };
        foreach (var fv in visibilities)
        foreach (var fc in creators)
        foreach (var iv in visibilities)
        foreach (var ic in creators)
        {
            yield return [fv, fc, iv, ic];
        }
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void TheGuard_AllowsAMerge_ExactlyWhenTheTargetsAudienceIsInsideTheMergedOnes(
        Visibility fromVisibility, int fromCreator, Visibility intoVisibility, int intoCreator)
    {
        var from = AnEntry(fromVisibility, Members[fromCreator]);
        var into = AnEntry(intoVisibility, Members[intoCreator]);

        var expected = Members.Where(m => EntryVisibility.CanSee(into, m)).All(m => EntryVisibility.CanSee(from, m));

        EntryMerge.RevealsNothing(from, into, Members).Should().Be(expected);
        (EntryMerge.Check(from, into, Members) == EntryMergeError.WouldReveal).Should().Be(!expected);
        EntryMerge.WhoLoses(from, into, Members).Should().BeEquivalentTo(
            Members.Where(m => EntryVisibility.CanSee(from, m) && !EntryVisibility.CanSee(into, m)));
    }

    [Fact]
    public void ADmEntry_IntoAnEveryoneEntry_IsRefused_AndTheOtherWay_LosesThePlayers()
    {
        var glasstaff = AnEntry(Visibility.DM, Dm);
        var iarno = AnEntry(Visibility.Everyone, Dm);

        EntryMerge.Check(glasstaff, iarno, Members).Should().Be(EntryMergeError.WouldReveal);
        EntryMerge.Check(iarno, glasstaff, Members).Should().Be(EntryMergeError.None);
        EntryMerge.WhoLoses(iarno, glasstaff, Members).Should().BeEquivalentTo([PlayerA, PlayerB]);
    }

    [Fact]
    public void AClaimedEntry_NeedsACharacter_UnclaimedOrWithTheSameClaimer()
    {
        var pc = AnEntry(Visibility.Everyone, PlayerA) with { ClaimedByMemberId = PlayerA.MemberId };

        EntryMerge.Check(pc, AnEntry(Visibility.Everyone, PlayerA, EntryKind.Place), Members).Should().Be(EntryMergeError.ClaimNeedsCharacter);
        EntryMerge.Check(pc, AnEntry(Visibility.Everyone, PlayerA) with { ClaimedByMemberId = PlayerB.MemberId }, Members)
            .Should().Be(EntryMergeError.ClaimedByAnother);
        EntryMerge.Check(pc, AnEntry(Visibility.Everyone, PlayerA) with { ClaimedByMemberId = PlayerA.MemberId }, Members)
            .Should().Be(EntryMergeError.None);
        EntryMerge.Check(pc, AnEntry(Visibility.Everyone, PlayerB), Members).Should().Be(EntryMergeError.None);
    }

    [Fact]
    public void Stats_Move_OnlyToATargetWithoutStats_AndNeverToMoreReaders()
    {
        var stats = new Stats { Ac = 12 };
        var npc = AnEntry(Visibility.Everyone, Dm) with { Stats = stats };
        var pc = AnEntry(Visibility.Everyone, PlayerA) with { Stats = stats, ClaimedByMemberId = PlayerA.MemberId };
        var bare = AnEntry(Visibility.Everyone, Dm);
        var claimedBare = bare with { ClaimedByMemberId = PlayerA.MemberId };

        EntryMerge.AdoptedStats(npc, bare).Should().Be(stats, "the DMs read both");
        EntryMerge.AdoptedStats(npc, claimedBare).Should().BeNull("a DM-only stat line would reach the players");
        EntryMerge.AdoptedStats(pc, bare).Should().Be(stats, "the claim moves too");
        EntryMerge.AdoptedStats(npc, bare with { Stats = new Stats { Ac = 1 } }).Should().BeNull("the target keeps its own");
        EntryMerge.AdoptedStats(npc, AnEntry(Visibility.Everyone, Dm, EntryKind.Place)).Should().BeNull();
    }
}
