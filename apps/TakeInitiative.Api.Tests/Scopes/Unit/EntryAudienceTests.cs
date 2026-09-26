using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Tests.Integration;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>
/// The push rule (<see cref="EntryAudience"/>) against the read rule
/// (<see cref="EntryVisibility"/>) and the edit rules (<see cref="EntryPermissions"/>).
/// If the first two drift apart, a member is pushed an entry they cannot read, or misses
/// one they can (invariant 5).
/// </summary>
public class EntryAudienceTests
{
    private static readonly Guid CampaignId = Guid.NewGuid();
    private static readonly Guid CreatorId = Guid.NewGuid();

    private static Entry AnEntry(Visibility visibility, EditAccess editAccess = EditAccess.Anyone) => new()
    {
        Id = Guid.NewGuid(),
        CampaignId = CampaignId,
        CreatorMemberId = CreatorId,
        Name = "Gundren",
        Kind = EntryKind.Character,
        Visibility = visibility,
        EditAccess = editAccess,
    };

    private static Member Viewer(Role role, bool isCreator) => new()
    {
        MemberId = isCreator ? CreatorId : Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Role = role,
        JoinedAt = DateTimeOffset.UnixEpoch,
    };

    public static TheoryData<Visibility, string[]> Table => new()
    {
        { Visibility.Everyone, [$"campaign:{CampaignId}"] },
        { Visibility.DM, [$"campaign:{CampaignId}:dm", $"member:{CreatorId}"] },
        { Visibility.Me, [$"member:{CreatorId}"] },
    };

    [Theory]
    [MemberData(nameof(Table))]
    public void Groups_FollowTheAudienceTable(Visibility visibility, string[] expected)
        => EntryAudience.Groups(AnEntry(visibility)).Should().BeEquivalentTo(expected);

    [Theory]
    [InlineData(Visibility.Everyone, Role.Player, true, true)]
    [InlineData(Visibility.Everyone, Role.DM, false, true)]
    [InlineData(Visibility.Everyone, Role.Player, false, true)]
    [InlineData(Visibility.DM, Role.Player, true, true)]
    [InlineData(Visibility.DM, Role.DM, false, true)]
    [InlineData(Visibility.DM, Role.Player, false, false)]
    [InlineData(Visibility.Me, Role.Player, true, true)]
    [InlineData(Visibility.Me, Role.DM, false, false)]
    [InlineData(Visibility.Me, Role.Player, false, false)]
    public void CanSee_FollowsTheVisibilityTable(Visibility visibility, Role role, bool isCreator, bool expected)
        => EntryVisibility.CanSee(AnEntry(visibility), Viewer(role, isCreator)).Should().Be(expected);

    [Fact]
    public void AViewerIsInTheAudience_ExactlyWhenTheyCanSeeTheEntry()
    {
        var cases = 0;
        foreach (var visibility in Enum.GetValues<Visibility>())
        foreach (var role in Enum.GetValues<Role>())
        foreach (var isCreator in new[] { false, true })
        {
            var entry = AnEntry(visibility);
            var viewer = Viewer(role, isCreator);

            var canSee = EntryVisibility.CanSee(entry, viewer);
            var pushed = EntryAudience.Groups(entry).Intersect(CampaignGroups.Of(CampaignId, viewer)).Any();

            pushed.Should().Be(canSee, $"{visibility}, {role}, creator {isCreator}");
            // The query form of the read rule agrees with the single-entry form.
            EntryVisibility.VisibleTo(viewer).Compile()(entry).Should().Be(canSee);
            cases++;
        }
        cases.Should().Be(Enum.GetValues<Visibility>().Length * Enum.GetValues<Role>().Length * 2);
    }

    [Fact]
    public void AnAudience_NeverIncludesTheWholeCampaign_UnlessEveryoneMaySeeIt()
    {
        foreach (var visibility in Enum.GetValues<Visibility>())
        {
            EntryAudience.Groups(AnEntry(visibility)).Contains($"campaign:{CampaignId}")
                .Should().Be(visibility == Visibility.Everyone);
        }
    }

    [Theory]
    [InlineData(EditAccess.Anyone, Role.Player, false, true, false)]
    [InlineData(EditAccess.Anyone, Role.Player, true, true, true)]
    [InlineData(EditAccess.Anyone, Role.DM, false, true, true)]
    [InlineData(EditAccess.OnlyMe, Role.Player, false, false, false)]
    [InlineData(EditAccess.OnlyMe, Role.Player, true, true, true)]
    [InlineData(EditAccess.OnlyMe, Role.DM, false, true, true)]
    public void Permissions_FollowTheTable(EditAccess editAccess, Role role, bool isCreator, bool canEdit, bool canChangeAccess)
    {
        var entry = AnEntry(Visibility.Everyone, editAccess);
        var member = Viewer(role, isCreator);
        EntryPermissions.CanEdit(entry, member).Should().Be(canEdit);
        EntryPermissions.CanChangeAccess(entry, member).Should().Be(canChangeAccess);
    }

    /// <summary>
    /// Every visibility change, replayed per viewer: a viewer who could see the entry before
    /// holds it, applies the messages that reach one of their groups in order, and must end
    /// holding it exactly when they can see it after. A message never reaches a viewer who
    /// can see it neither before nor after, and a removal only reaches one who could before.
    /// </summary>
    [Fact]
    public async Task AMove_LeavesEveryViewerWithTheEntryExactlyWhenTheyCanSeeIt()
    {
        var moves = 0;
        foreach (var beforeVisibility in Enum.GetValues<Visibility>())
        foreach (var afterVisibility in Enum.GetValues<Visibility>())
        {
            var before = AnEntry(beforeVisibility);
            var after = before with { Visibility = afterVisibility };
            var hub = new RecordingHubContext();
            await hub.NotifyEntryMoved(before, after);
            moves++;

            foreach (var role in Enum.GetValues<Role>())
            foreach (var isCreator in new[] { false, true })
            {
                var viewer = Viewer(role, isCreator);
                var viewerGroups = CampaignGroups.Of(CampaignId, viewer);
                var because = $"{beforeVisibility} -> {afterVisibility}, {role}, creator {isCreator}";
                var sawBefore = EntryVisibility.CanSee(before, viewer);
                var seesAfter = EntryVisibility.CanSee(after, viewer);

                var holds = sawBefore;
                foreach (var message in hub.Messages.Where(m => m.Groups.Intersect(viewerGroups).Any()))
                {
                    (sawBefore || seesAfter).Should().BeTrue(because);
                    if (message.Method == CampaignHubMessages.EntryRemoved)
                    {
                        sawBefore.Should().BeTrue(because);
                        message.Payload.Should().Be(new EntryRemovedMessage(before.Id));
                        holds = false;
                    }
                    else
                    {
                        message.Method.Should().Be(CampaignHubMessages.EntryUpserted);
                        seesAfter.Should().BeTrue(because);
                        holds = true;
                    }
                }
                holds.Should().Be(seesAfter, because);
            }
        }
        moves.Should().Be(9);
    }
}
