using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;

namespace TakeInitiative.Api.Tests.Integration.Features.Entries;

/// <summary>
/// What each entry write pushes through <c>CampaignHub</c>: the message names, the groups
/// and the order (step 15a.7). The creator is <see cref="Users.Player"/> unless a test says
/// otherwise; <see cref="Users.DM"/> owns the campaign.
/// </summary>
public class EntryHubTests(RecordingHubFixture fixture) : IClassFixture<RecordingHubFixture>
{
    private sealed record Groups(TestCampaign Campaign)
    {
        public string All => CampaignGroups.Campaign(Campaign.Id);
        public string Dms => CampaignGroups.Dms(Campaign.Id);
        public string Creator => CampaignGroups.Member(Campaign.PlayerMemberId);
    }

    private async Task<(TestCampaign Campaign, Groups Groups)> NewCampaign(string name)
    {
        var campaign = await TestCampaign.Create(fixture, name, withSecondPlayer: false);
        return (campaign, new Groups(campaign));
    }

    private async Task<IReadOnlyList<HubMessage>> Pushed(Func<Task> act)
    {
        var mark = fixture.Hub.Messages.Count;
        await act();
        return fixture.Hub.Messages.Skip(mark).ToList();
    }

    private async Task<EntryResponse> Create(TestCampaign campaign, Visibility visibility, string name = "Gundren")
    {
        fixture.LoginAsUser(Users.Player);
        var entry = await fixture.PostEntry(campaign.Id, name, EntryKind.Character, visibility);
        entry.Should().Succeed();
        return entry.Value;
    }

    private static void ShouldBeUpsert(HubMessage message, Guid entryId, params string[] groups)
    {
        message.Method.Should().Be(CampaignHubMessages.EntryUpserted);
        message.Groups.Should().BeEquivalentTo(groups);
        message.Payload.Should().BeOfType<EntrySummaryResponse>().Which.Id.Should().Be(entryId);
    }

    private static void ShouldBeRemoval(HubMessage message, Guid entryId, params string[] groups)
    {
        message.Method.Should().Be(CampaignHubMessages.EntryRemoved);
        message.Groups.Should().BeEquivalentTo(groups);
        message.Payload.Should().Be(new EntryRemovedMessage(entryId));
    }

    [Fact]
    public async Task Create_PushesTheEntryToItsAudienceOnly()
    {
        var (campaign, g) = await NewCampaign("Entry hub create");

        EntryResponse everyone = null!, dm = null!, me = null!;
        var toEveryone = await Pushed(async () => everyone = await Create(campaign, Visibility.Everyone, "A"));
        var toDm = await Pushed(async () => dm = await Create(campaign, Visibility.DM, "B"));
        var toMe = await Pushed(async () => me = await Create(campaign, Visibility.Me, "C"));

        ShouldBeUpsert(toEveryone.Should().ContainSingle().Subject, everyone.Id, g.All);
        ShouldBeUpsert(toDm.Should().ContainSingle().Subject, dm.Id, g.Dms, g.Creator);
        ShouldBeUpsert(toMe.Should().ContainSingle().Subject, me.Id, g.Creator);
        toDm.Concat(toMe).Should().NotContain(m => m.Groups.Contains(g.All), "a DM or Me entry never goes to the whole campaign");
    }

    [Fact]
    public async Task Rename_PushesTheRenamedEntry_AndAnUnchangedRenamePushesNothing()
    {
        var (campaign, g) = await NewCampaign("Entry hub rename");
        var entry = await Create(campaign, Visibility.DM);

        var renamed = await Pushed(async () => (await fixture.PutEntryName(campaign.Id, entry.Id, "Gundren Rockseeker")).Should().Succeed());
        var unchanged = await Pushed(async () => (await fixture.PutEntryName(campaign.Id, entry.Id, "Gundren Rockseeker")).Should().Succeed());

        ShouldBeUpsert(renamed.Should().ContainSingle().Subject, entry.Id, g.Dms, g.Creator);
        renamed.Single().Payload.Should().BeOfType<EntrySummaryResponse>().Which.Name.Should().Be("Gundren Rockseeker");
        unchanged.Should().BeEmpty();
    }

    [Fact]
    public async Task KindAliasesAndEditAccess_PushTheEntryToItsAudience_AndNothingWhenUnchanged()
    {
        var (campaign, g) = await NewCampaign("Entry hub other puts");
        var entry = await Create(campaign, Visibility.Me);

        var changed = await Pushed(async () =>
        {
            (await fixture.PutEntryKind(campaign.Id, entry.Id, EntryKind.Place)).Should().Succeed();
            (await fixture.PutEntryAliases(campaign.Id, entry.Id, "One", "Two")).Should().Succeed();
            (await fixture.PutEntryEditAccess(campaign.Id, entry.Id, EditAccess.OnlyMe)).Should().Succeed();
        });
        var unchanged = await Pushed(async () =>
        {
            (await fixture.PutEntryKind(campaign.Id, entry.Id, EntryKind.Place)).Should().Succeed();
            (await fixture.PutEntryAliases(campaign.Id, entry.Id, "Two", " One ")).Should().Succeed();
            (await fixture.PutEntryEditAccess(campaign.Id, entry.Id, EditAccess.OnlyMe)).Should().Succeed();
            (await fixture.PutEntryVisibility(campaign.Id, entry.Id, Visibility.Me)).Should().Succeed();
        });

        // One push per request, however many alias events it appended.
        changed.Should().HaveCount(3).And.AllSatisfy(m => ShouldBeUpsert(m, entry.Id, g.Creator));
        unchanged.Should().BeEmpty();
    }

    public static TheoryData<Visibility, Visibility> Moves => new()
    {
        { Visibility.Everyone, Visibility.DM },
        { Visibility.Everyone, Visibility.Me },
        { Visibility.DM, Visibility.Everyone },
        { Visibility.DM, Visibility.Me },
        { Visibility.Me, Visibility.Everyone },
        { Visibility.Me, Visibility.DM },
    };

    [Theory]
    [MemberData(nameof(Moves))]
    public async Task EveryVisibilityMove_RemovesFromTheGroupsThatLoseIt_ThenUpsertsToTheNewAudience(Visibility from, Visibility to)
    {
        var (campaign, g) = await NewCampaign($"Entry hub move {from} {to}");
        var entry = await Create(campaign, from);

        var pushed = await Pushed(async () => (await fixture.PutEntryVisibility(campaign.Id, entry.Id, to)).Should().Succeed());

        string[] Audience(Visibility v) => v switch
        {
            Visibility.Everyone => [g.All],
            Visibility.DM => [g.Dms, g.Creator],
            _ => [g.Creator],
        };
        var losing = to == Visibility.Everyone ? [] : Audience(from).Except(Audience(to)).ToArray();

        if (losing.Length > 0)
        {
            pushed.Should().HaveCount(2);
            ShouldBeRemoval(pushed[0], entry.Id, losing);
            ShouldBeUpsert(pushed[1], entry.Id, Audience(to));
        }
        else
        {
            ShouldBeUpsert(pushed.Should().ContainSingle().Subject, entry.Id, Audience(to));
        }
        if (to != Visibility.Everyone)
        {
            pushed.Last().Groups.Should().NotContain(g.All, "a narrowed entry's upsert never goes to the whole campaign");
        }
    }
}
