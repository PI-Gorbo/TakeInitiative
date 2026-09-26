using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Sessions;
using TakeInitiative.Api.Tests.Integration;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>
/// The push rule (<see cref="SessionNoteAudience"/>) against the read rule
/// (<see cref="SessionNoteVisibility"/>). If these drift apart, a member is pushed a note
/// they cannot read, or misses one they can (invariant 5).
/// </summary>
public class SessionNoteAudienceTests
{
    private static readonly Guid CampaignId = Guid.NewGuid();
    private static readonly Guid AuthorId = Guid.NewGuid();

    private static SessionNote Note(Visibility visibility, bool hidden) => new()
    {
        Id = Guid.NewGuid(),
        CampaignId = CampaignId,
        SessionId = Guid.NewGuid(),
        AuthorMemberId = AuthorId,
        Text = "A note",
        Visibility = visibility,
        IsHidden = hidden,
    };

    public static TheoryData<Visibility, bool, string[]> Table => new()
    {
        { Visibility.Everyone, false, [$"campaign:{CampaignId}"] },
        { Visibility.Everyone, true, [$"campaign:{CampaignId}:dm", $"member:{AuthorId}"] },
        { Visibility.DM, false, [$"campaign:{CampaignId}:dm", $"member:{AuthorId}"] },
        { Visibility.DM, true, [$"campaign:{CampaignId}:dm", $"member:{AuthorId}"] },
        { Visibility.Me, false, [$"member:{AuthorId}"] },
        { Visibility.Me, true, [$"member:{AuthorId}"] },
    };

    [Theory]
    [MemberData(nameof(Table))]
    public void Groups_FollowTheAudienceTable(Visibility visibility, bool hidden, string[] expected)
        => SessionNoteAudience.Groups(Note(visibility, hidden), CampaignId).Should().BeEquivalentTo(expected);

    [Fact]
    public void AViewerIsInTheAudience_ExactlyWhenTheyCanSeeTheNote()
    {
        var cases = 0;
        foreach (var visibility in Enum.GetValues<Visibility>())
        foreach (var hidden in new[] { false, true })
        foreach (var role in Enum.GetValues<Role>())
        foreach (var isAuthor in new[] { false, true })
        {
            var note = Note(visibility, hidden);
            var viewer = new Member { MemberId = isAuthor ? AuthorId : Guid.NewGuid(), UserId = Guid.NewGuid(), Role = role, JoinedAt = DateTimeOffset.UnixEpoch };

            var canSee = SessionNoteVisibility.CanSee(note, viewer);
            var pushed = SessionNoteAudience.Groups(note, CampaignId).Intersect(CampaignGroups.Of(CampaignId, viewer)).Any();

            pushed.Should().Be(canSee, $"{visibility}, hidden {hidden}, {role}, author {isAuthor}");
            // The query form of the read rule agrees with the single-note form.
            SessionNoteVisibility.VisibleTo(viewer).Compile()(note).Should().Be(canSee);
            cases++;
        }
        cases.Should().Be(Enum.GetValues<Visibility>().Length * 2 * Enum.GetValues<Role>().Length * 2);
    }

    [Fact]
    public void AnAudience_NeverIncludesTheWholeCampaign_UnlessEveryoneMaySeeIt()
    {
        foreach (var visibility in Enum.GetValues<Visibility>())
        foreach (var hidden in new[] { false, true })
        {
            var groups = SessionNoteAudience.Groups(Note(visibility, hidden), CampaignId);
            groups.Contains($"campaign:{CampaignId}").Should().Be(visibility == Visibility.Everyone && !hidden);
        }
    }

    /// <summary>
    /// Every visibility change and hide, replayed per viewer: a viewer who could see the note
    /// before holds it, applies the messages that reach one of their groups in order, and must
    /// end holding it exactly when they can see it after. A message never reaches a viewer
    /// who can see the note neither before nor after, and a removal only reaches a viewer who
    /// could see it before.
    /// </summary>
    [Fact]
    public async Task AMove_LeavesEveryViewerWithTheNoteExactlyWhenTheyCanSeeIt()
    {
        var states = Enum.GetValues<Visibility>().SelectMany(v => new[] { (v, false), (v, true) }).ToList();
        var moves = 0;
        foreach (var (beforeVisibility, beforeHidden) in states)
        foreach (var (afterVisibility, afterHidden) in states)
        {
            var before = Note(beforeVisibility, beforeHidden);
            var after = before with { Visibility = afterVisibility, IsHidden = afterHidden };
            var hub = new RecordingHubContext();
            await hub.NotifySessionNoteMoved(before, after);
            moves++;

            foreach (var role in Enum.GetValues<Role>())
            foreach (var isAuthor in new[] { false, true })
            {
                var viewer = new Member { MemberId = isAuthor ? AuthorId : Guid.NewGuid(), UserId = Guid.NewGuid(), Role = role, JoinedAt = DateTimeOffset.UnixEpoch };
                var viewerGroups = CampaignGroups.Of(CampaignId, viewer);
                var because = $"{beforeVisibility}/{beforeHidden} -> {afterVisibility}/{afterHidden}, {role}, author {isAuthor}";
                var sawBefore = SessionNoteVisibility.CanSee(before, viewer);
                var seesAfter = SessionNoteVisibility.CanSee(after, viewer);

                var holds = sawBefore;
                foreach (var message in hub.Messages.Where(m => m.Groups.Intersect(viewerGroups).Any()))
                {
                    (sawBefore || seesAfter).Should().BeTrue(because);
                    if (message.Method == CampaignHubMessages.SessionNoteRemoved)
                    {
                        sawBefore.Should().BeTrue(because);
                        holds = false;
                    }
                    else
                    {
                        message.Method.Should().Be(CampaignHubMessages.SessionNoteUpserted);
                        seesAfter.Should().BeTrue(because);
                        holds = true;
                    }
                }
                holds.Should().Be(seesAfter, because);
            }
        }
        moves.Should().Be(36);
    }
}
