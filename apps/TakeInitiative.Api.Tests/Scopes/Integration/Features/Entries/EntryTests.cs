using System.Text.Json;
using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;

namespace TakeInitiative.Api.Tests.Integration.Features.Entries;

/// <summary>
/// The entry stream and its endpoints (step 15a). <see cref="Users.DM"/> owns the campaign,
/// <see cref="Users.Player"/> creates entries unless a test says otherwise, and
/// <see cref="Users.Outsider"/> is the second player.
/// </summary>
public class EntryTests(AuthenticatedWebAppWithDatabaseFixture fixture) : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    private static string Url(Guid campaignId, Guid? entryId = null, string? part = null)
        => $"/api/campaigns/{campaignId}/entries" + (entryId is null ? "" : $"/{entryId}") + (part is null ? "" : $"/{part}");

    private async Task<(TestCampaign Campaign, EntryResponse Entry)> CampaignWithEntry(
        string name, Visibility visibility = Visibility.Everyone, bool withSecondPlayer = true)
    {
        var campaign = await TestCampaign.Create(fixture, name, withSecondPlayer);
        fixture.LoginAsUser(Users.Player);
        var entry = await fixture.PostEntry(campaign.Id, "Gundren", EntryKind.Character, visibility);
        entry.Should().Succeed();
        return (campaign, entry.Value);
    }

    [Fact]
    public async Task Create_ThenRead_ReturnsTheEntry()
    {
        var (campaign, created) = await CampaignWithEntry("Entry create");

        created.Name.Should().Be("Gundren");
        created.Kind.Should().Be(EntryKind.Character);
        created.Visibility.Should().Be(Visibility.Everyone);
        created.EditAccess.Should().Be(EditAccess.Anyone);
        created.CreatorMemberId.Should().Be(campaign.PlayerMemberId);
        created.Aliases.Should().BeEmpty();
        created.UpdatedAt.Should().Be(created.CreatedAt);

        fixture.LoginAsUser(Users.DM);
        var read = await fixture.GetEntry(campaign.Id, created.Id);
        read.Should().Succeed();
        read.Value.Should().BeEquivalentTo(created);

        var list = await fixture.GetEntries(campaign.Id);
        list.Should().Succeed();
        list.Value.Entries.Should().ContainSingle().Which.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task Create_TrimsTheName()
    {
        var campaign = await TestCampaign.Create(fixture, "Entry trim");
        fixture.LoginAsUser(Users.Player);
        var entry = await fixture.PostEntry(campaign.Id, "  Phandalin  ", EntryKind.Place);
        entry.Should().Succeed();
        entry.Value.Name.Should().Be("Phandalin");
    }

    [Fact]
    public async Task EveryPut_AppendsOneEvent_AndAnUnchangedValueAppendsNothing()
    {
        var (campaign, entry) = await CampaignWithEntry("Entry puts");

        (await fixture.PutEntryName(campaign.Id, entry.Id, "Gundren Rockseeker")).Value.Name.Should().Be("Gundren Rockseeker");
        (await fixture.PutEntryName(campaign.Id, entry.Id, " Gundren Rockseeker ")).Should().Succeed();
        (await fixture.PutEntryKind(campaign.Id, entry.Id, EntryKind.Faction)).Value.Kind.Should().Be(EntryKind.Faction);
        (await fixture.PutEntryKind(campaign.Id, entry.Id, EntryKind.Faction)).Should().Succeed();
        (await fixture.PutEntryVisibility(campaign.Id, entry.Id, Visibility.DM)).Value.Visibility.Should().Be(Visibility.DM);
        (await fixture.PutEntryVisibility(campaign.Id, entry.Id, Visibility.DM)).Should().Succeed();
        var last = await fixture.PutEntryEditAccess(campaign.Id, entry.Id, EditAccess.OnlyMe);
        last.Value.EditAccess.Should().Be(EditAccess.OnlyMe);
        (await fixture.PutEntryEditAccess(campaign.Id, entry.Id, EditAccess.OnlyMe)).Should().Succeed();

        var events = await TestCampaign.EventsOf(fixture, entry.Id);
        events.Select(e => e.Data.GetType()).Should().Equal(
            typeof(EntryCreated), typeof(EntryRenamed), typeof(EntryKindChanged),
            typeof(EntryVisibilityChanged), typeof(EntryEditAccessChanged));
        last.Value.UpdatedAt.Should().BeAfter(last.Value.CreatedAt);
    }

    [Fact]
    public async Task Aliases_AppendOneEventPerDifference()
    {
        var (campaign, entry) = await CampaignWithEntry("Entry aliases");

        var first = await fixture.PutEntryAliases(campaign.Id, entry.Id, "Rockseeker", " Gundie ", "rockseeker", "gundren");
        first.Should().Succeed();
        // Trimmed, de-duplicated case-insensitively, and the name dropped.
        first.Value.Aliases.Should().Equal("Rockseeker", "Gundie");

        var second = await fixture.PutEntryAliases(campaign.Id, entry.Id, "Gundie", "The Dwarf", "Old G");
        second.Value.Aliases.Should().BeEquivalentTo(["Gundie", "The Dwarf", "Old G"]);
        (await fixture.PutEntryAliases(campaign.Id, entry.Id, "Old G", "The Dwarf", "Gundie")).Should().Succeed();

        var events = (await TestCampaign.EventsOf(fixture, entry.Id)).Skip(1).Select(e => e.Data).ToList();
        events.Should().Equal(
            new EntryAliasAdded(Actor.Member(campaign.PlayerMemberId), "Rockseeker"),
            new EntryAliasAdded(Actor.Member(campaign.PlayerMemberId), "Gundie"),
            new EntryAliasRemoved(Actor.Member(campaign.PlayerMemberId), "Rockseeker"),
            new EntryAliasAdded(Actor.Member(campaign.PlayerMemberId), "The Dwarf"),
            new EntryAliasAdded(Actor.Member(campaign.PlayerMemberId), "Old G"));
    }

    [Fact]
    public async Task Rename_ToAnAlias_RemovesThatAlias()
    {
        var (campaign, entry) = await CampaignWithEntry("Entry rename to alias");
        (await fixture.PutEntryAliases(campaign.Id, entry.Id, "Rockseeker", "Gundie")).Should().Succeed();

        var renamed = await fixture.PutEntryName(campaign.Id, entry.Id, "rockseeker");
        renamed.Should().Succeed();
        renamed.Value.Name.Should().Be("rockseeker");
        renamed.Value.Aliases.Should().Equal("Gundie");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validation_RejectsABlankName(string name)
    {
        var (campaign, entry) = await CampaignWithEntry($"Entry blank {name.Length}");
        await fixture.ExpectStatus(HttpMethod.Post, Url(campaign.Id), new { name, kind = "Character", visibility = "Everyone" }, 400);
        await fixture.ExpectStatus(HttpMethod.Put, Url(campaign.Id, entry.Id, "name"), new { name }, 400);
        await fixture.ExpectStatus(HttpMethod.Put, Url(campaign.Id, entry.Id, "aliases"), new { aliases = new[] { name } }, 400);
    }

    [Fact]
    public async Task Validation_RejectsLongNames_TooManyAliases_AndUnknownEnums()
    {
        var (campaign, entry) = await CampaignWithEntry("Entry validation");
        var tooLong = new string('a', Entry.NameMaxLength + 1);
        var longest = "  " + new string('b', Entry.NameMaxLength) + "  ";

        await fixture.ExpectStatus(HttpMethod.Post, Url(campaign.Id), new { name = tooLong, kind = "Character", visibility = "Everyone" }, 400);
        await fixture.ExpectStatus(HttpMethod.Post, Url(campaign.Id), new { name = longest, kind = "Character", visibility = "Everyone" }, 200);
        await fixture.ExpectStatus(HttpMethod.Put, Url(campaign.Id, entry.Id, "name"), new { name = tooLong }, 400);
        await fixture.ExpectStatus(HttpMethod.Put, Url(campaign.Id, entry.Id, "aliases"), new { aliases = new[] { tooLong } }, 400);

        var twentyOne = Enumerable.Range(1, Entry.MaxAliases + 1).Select(i => $"Alias {i}").ToArray();
        await fixture.ExpectStatus(HttpMethod.Put, Url(campaign.Id, entry.Id, "aliases"), new { aliases = twentyOne }, 400);
        // Case-insensitive duplicates do not count towards the limit.
        await fixture.ExpectStatus(HttpMethod.Put, Url(campaign.Id, entry.Id, "aliases"),
            new { aliases = twentyOne.Take(Entry.MaxAliases).Append("ALIAS 1").ToArray() }, 200);

        await fixture.ExpectStatus(HttpMethod.Post, Url(campaign.Id), new { name = "X", kind = "Monster", visibility = "Everyone" }, 400);
        await fixture.ExpectStatus(HttpMethod.Post, Url(campaign.Id), new { name = "X", kind = "Place", visibility = "Players" }, 400);
        await fixture.ExpectStatus(HttpMethod.Put, Url(campaign.Id, entry.Id, "kind"), new { kind = "Monster" }, 400);
        await fixture.ExpectStatus(HttpMethod.Put, Url(campaign.Id, entry.Id, "visibility"), new { visibility = "Players" }, 400);
        await fixture.ExpectStatus(HttpMethod.Put, Url(campaign.Id, entry.Id, "edit-access"), new { editAccess = "Nobody" }, 400);
    }

    [Fact]
    public async Task DuplicateName_IsAConflict_OnlyAgainstEntriesTheCallerCanSee()
    {
        var (campaign, gundren) = await CampaignWithEntry("Entry duplicates");
        (await fixture.PutEntryAliases(campaign.Id, gundren.Id, "Rockseeker")).Should().Succeed();

        fixture.LoginAsUser(Users.DM);
        var glasstaff = (await fixture.PostEntry(campaign.Id, "Glasstaff", EntryKind.Character, Visibility.DM)).Value;

        // The same name or an alias, in any case, of a visible entry: 409 with its id.
        foreach (var (name, existing) in new[] { ("gundren", gundren.Id), ("ROCKSEEKER", gundren.Id), ("Glasstaff", glasstaff.Id) })
        {
            var result = await fixture.AlbaHost.Scenario(_ =>
            {
                _.Post.Json(new { name, kind = "Character", visibility = "Everyone" }).ToUrl(Url(campaign.Id));
                _.StatusCodeShouldBe(409);
            });
            using var body = JsonDocument.Parse(await result.ReadAsTextAsync());
            body.RootElement.GetProperty("errors").GetProperty(PostEntry.ExistingEntryIdKey)[0].GetString()
                .Should().Be(existing.ToString());
        }

        // A player cannot see the DM-only Glasstaff, so theirs is created and nothing leaks.
        fixture.LoginAsUser(Users.Outsider);
        (await fixture.PostEntry(campaign.Id, "Glasstaff", EntryKind.Character, Visibility.DM)).Should().Succeed();
        // Two DM-only Glasstaffs by different creators now exist; the DM sees both.
        fixture.LoginAsUser(Users.DM);
        (await fixture.GetEntries(campaign.Id)).Value.Entries.Count(e => e.Name == "Glasstaff").Should().Be(2);
    }

    [Fact]
    public async Task AnOutsider_Gets403Everywhere()
    {
        var (campaign, entry) = await CampaignWithEntry("Entry outsider", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Outsider);

        await fixture.ExpectStatus(HttpMethod.Get, Url(campaign.Id), null, 403);
        await fixture.ExpectStatus(HttpMethod.Post, Url(campaign.Id), new { name = "X", kind = "Character", visibility = "Everyone" }, 403);
        await fixture.ExpectStatus(HttpMethod.Get, Url(campaign.Id, entry.Id), null, 403);
        await fixture.ExpectStatus(HttpMethod.Put, Url(campaign.Id, entry.Id, "name"), new { name = "Y" }, 403);
        await fixture.ExpectStatus(HttpMethod.Put, Url(campaign.Id, entry.Id, "kind"), new { kind = "Place" }, 403);
        await fixture.ExpectStatus(HttpMethod.Put, Url(campaign.Id, entry.Id, "aliases"), new { aliases = new[] { "Z" } }, 403);
        await fixture.ExpectStatus(HttpMethod.Put, Url(campaign.Id, entry.Id, "visibility"), new { visibility = "Me" }, 403);
        await fixture.ExpectStatus(HttpMethod.Put, Url(campaign.Id, entry.Id, "edit-access"), new { editAccess = "OnlyMe" }, 403);
    }

    [Fact]
    public async Task AnEntryOfAnotherCampaign_IsA404()
    {
        var (_, entry) = await CampaignWithEntry("Entry campaign A");
        var other = await TestCampaign.Create(fixture, "Entry campaign B");
        fixture.LoginAsUser(Users.DM);

        await fixture.ExpectStatus(HttpMethod.Get, Url(other.Id, entry.Id), null, 404);
        await fixture.ExpectStatus(HttpMethod.Put, Url(other.Id, entry.Id, "name"), new { name = "Y" }, 404);
    }

    [Fact]
    public async Task EveryEvent_CarriesAnActorAndACorrelationId()
    {
        var (campaign, entry) = await CampaignWithEntry("Entry provenance");
        (await fixture.PutEntryName(campaign.Id, entry.Id, "Gundren Rockseeker")).Should().Succeed();
        (await fixture.PutEntryKind(campaign.Id, entry.Id, EntryKind.Other)).Should().Succeed();
        (await fixture.PutEntryAliases(campaign.Id, entry.Id, "Rockseeker")).Should().Succeed();
        fixture.LoginAsUser(Users.DM);
        (await fixture.PutEntryAliases(campaign.Id, entry.Id)).Should().Succeed();
        (await fixture.PutEntryVisibility(campaign.Id, entry.Id, Visibility.DM)).Should().Succeed();
        (await fixture.PutEntryEditAccess(campaign.Id, entry.Id, EditAccess.OnlyMe)).Should().Succeed();

        var events = await TestCampaign.EventsOf(fixture, entry.Id);
        events.Select(e => e.Data.GetType()).Should().Equal(
            typeof(EntryCreated), typeof(EntryRenamed), typeof(EntryKindChanged), typeof(EntryAliasAdded),
            typeof(EntryAliasRemoved), typeof(EntryVisibilityChanged), typeof(EntryEditAccessChanged));
        events.Should().AllSatisfy(e =>
        {
            e.Data.Should().BeAssignableTo<IActorEvent>().Which.Actor.MemberId.Should().NotBeEmpty();
            e.CorrelationId.Should().NotBeNullOrWhiteSpace();
            e.Headers.Should().ContainKey("request");
        });
        events.Select(e => ((IActorEvent)e.Data).Actor.MemberId).Should().Equal(
            campaign.PlayerMemberId, campaign.PlayerMemberId, campaign.PlayerMemberId, campaign.PlayerMemberId,
            campaign.DmMemberId, campaign.DmMemberId, campaign.DmMemberId);
        events.Select(e => e.CorrelationId).Should().OnlyHaveUniqueItems();
        events[0].Data.Should().BeOfType<EntryCreated>().Which.CreatorMemberId.Should().Be(campaign.PlayerMemberId);
    }
}
