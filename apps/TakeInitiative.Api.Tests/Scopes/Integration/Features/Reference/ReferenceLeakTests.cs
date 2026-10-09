using System.Text.Json;
using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Search;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Reference;

/// <summary>
/// An NPC's source is its stat block (20b.4, invariant 8): "Mysterious Stranger ← SRD Vampire"
/// must not reach a player through any read, push or search while the entry is unclaimed. Each
/// check looks at the raw JSON, so a field that is there but ignored by the client still fails.
/// Claiming the entry shows the source to the claimer and the table, as it does the Stats.
/// </summary>
public class ReferenceLeakTests : IClassFixture<RecordingHubFixture>
{
    private readonly RecordingHubFixture fixture;

    public ReferenceLeakTests(RecordingHubFixture fixture)
    {
        this.fixture = fixture;
        UseRealDice(fixture);
    }

    /// <summary>What must never appear in a player's JSON: the provider, the item's id and its name.</summary>
    private static readonly string[] Secrets = ["srd52", "vampire", "Vampire", "SRD 5.2", "dndbeyond"];

    private async Task<(TestCampaign Campaign, Guid EntryId, IReadOnlyList<HubMessage> Pushed)> Stranger(string name)
    {
        var campaign = await TestCampaign.Create(fixture, name);
        var mark = fixture.Hub.Messages.Count;
        fixture.LoginAsUser(Users.DM);
        var reply = await fixture.Call(HttpMethod.Post, EntryFromReferenceTests.FromReferenceUrl(campaign.Id),
            new { provider = "srd52", itemId = "vampire", name = "Mysterious Stranger", visibility = "Everyone" }).Ok();
        var entry = reply.As<EntryResponse>();
        entry.Source!.ExternalId.Should().Be("vampire", "the DM who made it reads it");
        return (campaign, entry.Id, fixture.Hub.Messages.Skip(mark).ToList());
    }

    private async Task<string> Raw(Users who, string url)
    {
        fixture.LoginAsUser(who);
        var reply = await fixture.Call(HttpMethod.Get, url).Ok();
        return reply.Body;
    }

    private static void ShouldNotLeak(string json, string what)
    {
        foreach (var secret in Secrets)
        {
            json.Should().NotContain(secret, $"{what} must not name the source");
        }
        using var doc = JsonDocument.Parse(json);
        Keys(doc.RootElement).Should().NotContain("source", $"{what} has no source key at all");
    }

    private static IEnumerable<string> Keys(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().SelectMany(p => Keys(p.Value).Prepend(p.Name)),
        JsonValueKind.Array => element.EnumerateArray().SelectMany(Keys),
        _ => [],
    };

    [Fact]
    public async Task APlayer_NeverSeesAnUnclaimedEntrysSource()
    {
        var (campaign, entryId, pushed) = await Stranger("Reference leak: reads");

        foreach (var who in new[] { Users.Player, Users.Outsider })
        {
            ShouldNotLeak(await Raw(who, EntryUrl(campaign.Id, entryId)), "GET entry");
            ShouldNotLeak(await Raw(who, $"/api/campaigns/{campaign.Id}/entries"), "GET entries");
            ShouldNotLeak(await Raw(who, EntryUrl(campaign.Id, entryId, "history")), "GET entry history");
            var search = await Raw(who, SearchUrl(campaign.Id, "mysterious stranger", "entries"));
            search.Should().Contain(entryId.ToString(), "the control: the entry itself is found");
            ShouldNotLeak(search, "GET search");
        }

        // The DM reads it everywhere a source is shown.
        (await Raw(Users.DM, EntryUrl(campaign.Id, entryId))).Should().Contain("\"externalId\":\"vampire\"");
        fixture.LoginAsUser(Users.DM);
        var created = (await fixture.GetEntryHistory(campaign.Id, entryId)).Value.Items.First().Change;
        created.Type.Should().Be(EntryChangeType.Created);
        created.Source!.ExternalId.Should().Be("vampire");

        // The push goes to everyone who sees the entry, so it carries no source for anyone.
        var upserted = pushed.Should().ContainSingle(m => m.Method == CampaignHubMessages.EntryUpserted).Subject;
        ShouldNotLeak(JsonSerializer.Serialize(upserted.Payload, upserted.Payload!.GetType(), Web), "entryUpserted");
        pushed.Where(m => m.Method == CampaignHubMessages.EntryStatsChanged).Should().AllSatisfy(m =>
            m.Groups.Should().Equal(CampaignGroups.Member(campaign.DmMemberId)));
    }

    [Fact]
    public async Task Claiming_ShowsTheSource_ToTheClaimerAndTheTable()
    {
        var (campaign, entryId, _) = await Stranger("Reference leak: claim");

        fixture.LoginAsUser(Users.Player);
        var claimed = await fixture.PutEntryClaim(campaign.Id, entryId, campaign.PlayerMemberId);
        claimed.Should().Succeed();
        claimed.Value.Source!.ExternalId.Should().Be("vampire");

        fixture.LoginAsUser(Users.Outsider);
        (await fixture.GetEntry(campaign.Id, entryId)).Value.Source!.Name.Should().Be("Vampire");
        (await fixture.GetEntryHistory(campaign.Id, entryId)).Value.Items.First().Change.Source!.ExternalId.Should().Be("vampire");

        // Unclaiming hides it again.
        fixture.LoginAsUser(Users.Player);
        (await fixture.PutEntryClaim(campaign.Id, entryId, null)).Value.Source.Should().BeNull();
        ShouldNotLeak(await Raw(Users.Outsider, EntryUrl(campaign.Id, entryId)), "GET entry after the unclaim");
    }

    [Fact]
    public async Task AnotherKind_ShowsItsSource_ToEveryoneWhoSeesIt()
    {
        var (campaign, entryId, _) = await Stranger("Reference leak: kind");

        fixture.LoginAsUser(Users.DM);
        (await fixture.PutEntryKind(campaign.Id, entryId, EntryKind.Other)).Should().Succeed();

        fixture.LoginAsUser(Users.Player);
        (await fixture.GetEntry(campaign.Id, entryId)).Value.Source!.ExternalId.Should().Be("vampire");
    }
}
