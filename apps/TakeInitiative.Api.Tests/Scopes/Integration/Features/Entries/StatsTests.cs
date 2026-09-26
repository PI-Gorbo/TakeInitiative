using System.Text.Json;
using FakeItEasy;
using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using TakeInitiative.Utilities;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Entries;

/// <summary>
/// Stats (15g.3): who reads and writes them on claimed and unclaimed entries, the dice check,
/// Character only, and who is pinged. <see cref="Users.DM"/> is the DM, <see cref="Users.Player"/>
/// and <see cref="Users.Outsider"/> the players. The fixture's dice roller is a fake, so its
/// <c>Check</c> is pointed at the real one here.
/// </summary>
public class StatsTests : IClassFixture<RecordingHubFixture>
{
    private readonly RecordingHubFixture fixture;

    public StatsTests(RecordingHubFixture fixture)
    {
        this.fixture = fixture;
        var real = new DiceRoller(Random.Shared);
        A.CallTo(() => fixture.DiceRoller.Check(A<string>._)).ReturnsLazily((string roll) => real.Check(roll));
    }

    private async Task<EntryResponse> Entry(Users user, Guid campaignId, string name, EntryKind kind = EntryKind.Character)
    {
        fixture.LoginAsUser(user);
        var entry = await fixture.PostEntry(campaignId, name, kind);
        entry.Should().Succeed();
        return entry.Value;
    }

    private async Task<EntryResponse> Put(Users user, Guid campaignId, Guid entryId, string? initiative, string? maxHp, int? ac)
    {
        fixture.LoginAsUser(user);
        var saved = await fixture.PutEntryStats(campaignId, entryId, initiative, maxHp, ac);
        saved.Should().Succeed();
        return saved.Value;
    }

    private async Task<(int Status, string Body)> Refused(Users user, Guid campaignId, Guid entryId, object body)
    {
        fixture.LoginAsUser(user);
        return await fixture.Send(HttpMethod.Put, EntryUrl(campaignId, entryId, "stats"), body);
    }

    private async Task<StatsResponse?> Read(Users user, Guid campaignId, Guid entryId)
    {
        fixture.LoginAsUser(user);
        return (await fixture.GetEntry(campaignId, entryId)).Value.Stats;
    }

    [Fact]
    public async Task AnUnclaimedEntrysStats_AreTheDmsOnly()
    {
        var campaign = await TestCampaign.Create(fixture, "Stats NPC");
        var klarg = await Entry(Users.Player, campaign.Id, "Klarg");

        (await Refused(Users.Player, campaign.Id, klarg.Id, new { ac = 12 })).Status.Should().Be(403, "even its creator");
        var mark = fixture.Hub.Messages.Count;
        var saved = await Put(Users.DM, campaign.Id, klarg.Id, "1d20+1", "5d8+10", 12);

        saved.Stats.Should().Be(new StatsResponse { InitiativeRoll = "1d20+1", MaxHp = "5d8+10", Ac = 12 });
        saved.UpdatedAt.Should().Be(klarg.UpdatedAt, "the summary every viewer gets does not move");
        (await Read(Users.Player, campaign.Id, klarg.Id)).Should().BeNull();
        (await Read(Users.Outsider, campaign.Id, klarg.Id)).Should().BeNull();
        fixture.LoginAsUser(Users.Player);
        var raw = await fixture.AlbaHost.Scenario(_ => _.Get.Url(EntryUrl(campaign.Id, klarg.Id)));
        (await raw.ReadAsTextAsync()).Should().NotContain("5d8+10");

        var pushed = fixture.Hub.Messages.Skip(mark).ToList();
        var ping = pushed.Should().ContainSingle().Subject;
        ping.Method.Should().Be(CampaignHubMessages.EntryStatsChanged);
        ping.Groups.Should().Equal(CampaignGroups.Member(campaign.DmMemberId));
    }

    [Fact]
    public async Task AClaimedEntrysStats_AreReadByAll_AndWrittenByTheClaimerAndTheDms()
    {
        var campaign = await TestCampaign.Create(fixture, "Stats PC");
        var tordek = await Entry(Users.DM, campaign.Id, "Tordek");
        await Put(Users.DM, campaign.Id, tordek.Id, null, "12", 16);

        // Claiming shows the stats to the players, and pings them.
        var mark = fixture.Hub.Messages.Count;
        fixture.LoginAsUser(Users.Player);
        (await fixture.PutEntryClaim(campaign.Id, tordek.Id, campaign.PlayerMemberId)).Value.Stats!.Ac.Should().Be(16);
        var ping = fixture.Hub.Messages.Skip(mark).Should().ContainSingle(m => m.Method == CampaignHubMessages.EntryStatsChanged).Subject;
        ping.Groups.Should().BeEquivalentTo([CampaignGroups.Member(campaign.PlayerMemberId), CampaignGroups.Member(campaign.SecondPlayerMemberId!.Value)]);

        (await Put(Users.Player, campaign.Id, tordek.Id, "1d20+2", "3d10", 17)).Stats!.MaxHp.Should().Be("3d10");
        (await Read(Users.Outsider, campaign.Id, tordek.Id))!.InitiativeRoll.Should().Be("1d20+2");
        (await Refused(Users.Outsider, campaign.Id, tordek.Id, new { ac = 1 })).Status.Should().Be(403);
        (await Put(Users.DM, campaign.Id, tordek.Id, "1d20+2", "3d10", 18)).Stats!.Ac.Should().Be(18);

        // All null clears them; the same again appends nothing.
        (await Put(Users.Player, campaign.Id, tordek.Id, " ", null, null)).Stats.Should().BeNull();
        var events = (await TestCampaign.EventsOf(fixture, tordek.Id)).Count;
        await Put(Users.Player, campaign.Id, tordek.Id, null, null, null);
        (await TestCampaign.EventsOf(fixture, tordek.Id)).Should().HaveCount(events);
    }

    [Theory]
    [InlineData("maxHp", "2d")]
    [InlineData("maxHp", "hello")]
    [InlineData("initiativeRoll", "1d20+")]
    [InlineData("initiativeRoll", "0d6")]
    public async Task ABadDiceExpression_IsA400_WithTheDiceMessage(string field, string expression)
    {
        var campaign = await TestCampaign.Create(fixture, $"Stats dice {field} {expression}");
        var klarg = await Entry(Users.DM, campaign.Id, "Klarg");

        var (status, body) = await Refused(Users.DM, campaign.Id, klarg.Id, new Dictionary<string, object> { [field] = expression });

        status.Should().Be(400);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("errors").GetProperty(field)[0].GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PlainNumbers_AreExpressions_AndAcIsZeroTo99()
    {
        var campaign = await TestCampaign.Create(fixture, "Stats numbers");
        var klarg = await Entry(Users.DM, campaign.Id, "Klarg");

        (await Put(Users.DM, campaign.Id, klarg.Id, "3", "27", 0)).Stats.Should().Be(new StatsResponse { InitiativeRoll = "3", MaxHp = "27", Ac = 0 });
        (await Refused(Users.DM, campaign.Id, klarg.Id, new { ac = 100 })).Status.Should().Be(400);
        (await Refused(Users.DM, campaign.Id, klarg.Id, new { ac = -1 })).Status.Should().Be(400);
    }

    [Fact]
    public async Task OnlyACharacter_HasStats()
    {
        var campaign = await TestCampaign.Create(fixture, "Stats kind");
        var cart = await Entry(Users.DM, campaign.Id, "Wagon", EntryKind.Item);
        (await Refused(Users.DM, campaign.Id, cart.Id, new { ac = 10 })).Status.Should().Be(409);

        // Stats on a Character that becomes something else are not shown, and come back with the kind.
        var klarg = await Entry(Users.DM, campaign.Id, "Klarg");
        await Put(Users.DM, campaign.Id, klarg.Id, null, null, 12);
        fixture.LoginAsUser(Users.DM);
        (await fixture.PutEntryKind(campaign.Id, klarg.Id, EntryKind.Other)).Value.Stats.Should().BeNull();
        (await fixture.PutEntryKind(campaign.Id, klarg.Id, EntryKind.Character)).Value.Stats!.Ac.Should().Be(12);
    }
}
