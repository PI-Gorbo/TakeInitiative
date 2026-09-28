using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Combats;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;

namespace TakeInitiative.Api.Tests.Integration.Features.Combats;

/// <summary>
/// Adding, editing and removing combatants: 18a.4's table, row by row. <see cref="Users.DM"/>
/// owns each campaign; <see cref="Users.Player"/> has claimed Brynn; <see cref="Users.Outsider"/>
/// is a second player.
/// </summary>
public class CombatantTests : IClassFixture<RecordingHubFixture>
{
    private readonly RecordingHubFixture fixture;

    public CombatantTests(RecordingHubFixture fixture)
    {
        this.fixture = fixture;
        UseRealDice(fixture);
    }

    private async Task<(TestCampaign Campaign, CombatResponse Combat)> Setup(string name)
    {
        var campaign = await TestCampaign.Create(fixture, name);
        return (campaign, await fixture.CreateCombat(campaign.Id));
    }

    private async Task<CombatResponse> Started(TestCampaign campaign, CombatResponse combat)
    {
        await fixture.Start(combat.Id, new Dictionary<Guid, int>());
        fixture.LoginAsUser(Users.DM);
        return (await fixture.GetCombat(campaign.Id, combat.Id)).Combat;
    }

    // Add.

    [Fact]
    public async Task ADm_AddsEntriesAndPlainNames_WithTheDefaults()
    {
        var (campaign, combat) = await Setup("Combatants DM add");
        var goblin = await fixture.Entry(Users.DM, campaign.Id, "Goblin", stats: ("1d20+2", "7", 15));

        var after = await fixture.AddAsDm(campaign.Id, combat.Id,
            new { entryId = goblin.Id, count = 4 },
            new { name = "Bandit", maxHp = "11", ac = 12 });

        after.Combatants.Select(c => c.Name).Should().Equal("Goblin 1", "Goblin 2", "Goblin 3", "Goblin 4", "Bandit");
        after.Combatants.Should().OnlyContain(c => c.Waiting && c.Initiative == null);
        var g = after.Combatants[0];
        (g.EntryId, g.Hp, g.MaxHp, g.Ac, g.InitiativeRoll, g.PlayersSee, g.Hidden, g.Band)
            .Should().Be(((Guid?)goblin.Id, 7, 7, 15, "1d20+2", PlayersSee.Band, false, HpBand.Healthy));
        var bandit = after.Combatants[4];
        (bandit.EntryId, bandit.Hp, bandit.Ac, bandit.InitiativeRoll).Should().Be(((Guid?)null, 11, 12, "1d20"));

        // Numbering continues past the ones already there, in a later request too.
        (await fixture.AddAsDm(campaign.Id, combat.Id, new { entryId = goblin.Id })).Combatants
            .Select(c => c.Name).Should().Contain("Goblin 5");

        var events = await TestCampaign.EventsOf(fixture, combat.Id);
        events.Select(e => e.Data).OfType<CombatantsAdded>().Should().HaveCount(2)
            .And.OnlyContain(e => e.Actor.MemberId == campaign.DmMemberId);
    }

    [Fact]
    public async Task ADm_AddsToAnActiveCombat_AndTheLateJoinerWaits()
    {
        var (campaign, combat) = await Setup("Combatants DM active");
        combat = await Started(campaign, combat);

        var after = await fixture.AddAsDm(campaign.Id, combat.Id, new { name = "Wolf" });

        after.Status.Should().Be(CombatStatus.Active);
        after.Combatants.Should().ContainSingle().Which.Waiting.Should().BeTrue();
    }

    [Fact]
    public async Task ADmAddingAnEntryTheyCannotSee_OrOfAnotherCampaign_IsA404()
    {
        var (campaign, combat) = await Setup("Combatants DM entry 404");
        var secret = await fixture.Entry(Users.Player, campaign.Id, "Player's secret", Visibility.Me);
        var other = await TestCampaign.Create(fixture, "Combatants other campaign", withSecondPlayer: false);
        var elsewhere = await fixture.Entry(Users.DM, other.Id, "Elsewhere");

        fixture.LoginAsUser(Users.DM);
        (await fixture.AddCombatants(campaign.Id, combat.Id, new { entryId = secret.Id })).Status.Should().Be(404);
        (await fixture.AddCombatants(campaign.Id, combat.Id, new { entryId = elsewhere.Id })).Status.Should().Be(404);
    }

    [Theory]
    [InlineData("{\"combatants\":[]}")]
    [InlineData("{\"combatants\":[{}]}")]
    [InlineData("{\"combatants\":[{\"name\":\"Rat\",\"count\":21}]}")]
    [InlineData("{\"combatants\":[{\"name\":\"Rat\",\"count\":0}]}")]
    [InlineData("{\"combatants\":[{\"name\":\"Rat\",\"maxHp\":\"2d\"}]}")]
    [InlineData("{\"combatants\":[{\"name\":\"Rat\",\"initiativeRoll\":\"hello\"}]}")]
    [InlineData("{\"combatants\":[{\"name\":\"Rat\",\"ac\":100}]}")]
    public async Task ABadAdd_IsA400(string json)
    {
        var (campaign, combat) = await Setup($"Combatants bad add {json.GetHashCode()}");
        fixture.LoginAsUser(Users.DM);
        var body = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
        (await fixture.Call(HttpMethod.Post, CombatUrl(campaign.Id, combat.Id, "combatants"), body)).Status.Should().Be(400);
    }

    [Fact]
    public async Task ACombat_HoldsAtMost50()
    {
        var (campaign, combat) = await Setup("Combatants 50");
        await fixture.AddAsDm(campaign.Id, combat.Id, new { name = "Rat", count = 20 }, new { name = "Bat", count = 20 });

        fixture.LoginAsUser(Users.DM);
        (await fixture.AddCombatants(campaign.Id, combat.Id, new { name = "Cat", count = 11 })).Status.Should().Be(400);
        (await fixture.AddCombatants(campaign.Id, combat.Id, new { name = "Cat", count = 10 })).Status.Should().Be(200);
    }

    [Fact]
    public async Task APlayer_AddsTheirOwnCharacter_Once()
    {
        var (campaign, combat) = await Setup("Combatants player add");
        var brynn = await fixture.PlayerCharacter(campaign);
        combat = await Started(campaign, combat);

        fixture.LoginAsUser(Users.Player);
        var added = await fixture.AddCombatants(campaign.Id, combat.Id, new { entryId = brynn.Id });
        added.Status.Should().Be(200, added.Body);
        var row = added.Combat.Combatants.Should().ContainSingle().Subject;
        (row.Name, row.OwnerMemberId, row.PlayersSee, row.Hp, row.MaxHp, row.Ac, row.InitiativeRoll, row.Waiting)
            .Should().Be(("Brynn", (Guid?)campaign.PlayerMemberId, PlayersSee.Exact, 24, 24, 15, "1d20+3", true));

        var again = await fixture.AddCombatants(campaign.Id, combat.Id, new { entryId = brynn.Id });
        again.Status.Should().Be(409);
    }

    [Fact]
    public async Task APlayer_CannotAddSomeoneElsesCharacter_OrAMonster()
    {
        var (campaign, combat) = await Setup("Combatants player others");
        var theirs = await fixture.Entry(Users.Outsider, campaign.Id, "Theirs");
        fixture.LoginAsUser(Users.Outsider);
        (await fixture.PutEntryClaim(campaign.Id, theirs.Id, campaign.SecondPlayerMemberId)).Should().Succeed();
        var goblin = await fixture.Entry(Users.DM, campaign.Id, "Goblin");
        combat = await Started(campaign, combat);

        fixture.LoginAsUser(Users.Player);
        (await fixture.AddCombatants(campaign.Id, combat.Id, new { entryId = theirs.Id })).Status.Should().Be(403);
        (await fixture.AddCombatants(campaign.Id, combat.Id, new { entryId = goblin.Id })).Status.Should().Be(403);
        (await fixture.AddCombatants(campaign.Id, combat.Id, new { name = "Bandit" })).Status.Should().Be(403);
    }

    [Fact]
    public async Task APlayer_AddsOneAtATime_WithNothingElseSet()
    {
        var (campaign, combat) = await Setup("Combatants player one");
        var brynn = await fixture.PlayerCharacter(campaign);
        var other = await fixture.PlayerCharacter(campaign, "Brynn's twin");
        combat = await Started(campaign, combat);

        fixture.LoginAsUser(Users.Player);
        (await fixture.AddCombatants(campaign.Id, combat.Id, new { entryId = brynn.Id }, new { entryId = other.Id })).Status.Should().Be(403);
        (await fixture.AddCombatants(campaign.Id, combat.Id, new { entryId = brynn.Id, count = 2 })).Status.Should().Be(403);
        (await fixture.AddCombatants(campaign.Id, combat.Id, new { entryId = brynn.Id, hidden = true })).Status.Should().Be(403);
        (await fixture.AddCombatants(campaign.Id, combat.Id, new { entryId = brynn.Id, maxHp = "999" })).Status.Should().Be(403);
    }

    [Fact]
    public async Task APlayer_AddingToADraft_IsA404()
    {
        var (campaign, combat) = await Setup("Combatants player draft");
        var brynn = await fixture.PlayerCharacter(campaign);

        fixture.LoginAsUser(Users.Player);
        (await fixture.AddCombatants(campaign.Id, combat.Id, new { entryId = brynn.Id })).Status.Should().Be(404);
    }

    // Edit.

    [Fact]
    public async Task ADm_EditsEveryField_AndTheSameStateAppendsNothing()
    {
        var (campaign, combat) = await Setup("Combatants DM edit");
        var goblin = (await fixture.AddAsDm(campaign.Id, combat.Id, new { name = "Goblin", maxHp = "7" })).Combatants[0];

        fixture.LoginAsUser(Users.DM);
        var body = Body(goblin, b =>
        {
            b["name"] = " Goblin Boss ";
            b["initiative"] = 12;
            b["hp"] = 3;
            b["maxHp"] = 20;
            b["ac"] = 17;
            b["hidden"] = true;
            b["playersSee"] = "Nothing";
            b["conditions"] = new[] { new { label = " Prone ", note = (string?)" " }, new { label = "Poisoned", note = (string?)"1 hour" } };
        });
        var edited = await fixture.PutCombatant(campaign.Id, combat.Id, goblin.Id, body);
        edited.Status.Should().Be(200, edited.Body);
        var row = edited.Combat.Combatants.Single();
        (row.Name, row.Initiative, row.Waiting, row.Hp, row.MaxHp, row.Ac, row.Hidden, row.PlayersSee, row.Band)
            .Should().Be(("Goblin Boss", (int?)12, false, (int?)3, (int?)20, (int?)17, true, PlayersSee.Nothing, (HpBand?)HpBand.Bloodied));
        row.Conditions.Should().Equal(new Condition("Prone", null), new Condition("Poisoned", "1 hour"));

        var count = (await TestCampaign.EventsOf(fixture, combat.Id)).Count;
        (await fixture.PutCombatant(campaign.Id, combat.Id, goblin.Id, Body(row))).Status.Should().Be(200);
        (await TestCampaign.EventsOf(fixture, combat.Id)).Should().HaveCount(count, "the same state is no edit");

        // Clearing the initiative makes it wait again.
        (await fixture.PutCombatant(campaign.Id, combat.Id, goblin.Id, Body(row, b => b["initiative"] = null)))
            .Combat.Combatants.Single().Waiting.Should().BeTrue();
    }

    [Theory]
    [InlineData("hp", -1000)]
    [InlineData("hp", 10000)]
    [InlineData("maxHp", 0)]
    [InlineData("ac", 100)]
    [InlineData("initiative", 100)]
    [InlineData("initiative", -100)]
    public async Task AnEditOutOfRange_IsA400(string field, int value)
    {
        var (campaign, combat) = await Setup($"Combatants range {field} {value}");
        var goblin = (await fixture.AddAsDm(campaign.Id, combat.Id, new { name = "Goblin" })).Combatants[0];
        fixture.LoginAsUser(Users.DM);
        (await fixture.PutCombatant(campaign.Id, combat.Id, goblin.Id, Body(goblin, b => b[field] = value))).Status.Should().Be(400);
    }

    [Fact]
    public async Task Conditions_HaveLimits()
    {
        var (campaign, combat) = await Setup("Combatants conditions");
        var goblin = (await fixture.AddAsDm(campaign.Id, combat.Id, new { name = "Goblin" })).Combatants[0];
        fixture.LoginAsUser(Users.DM);

        var tooMany = Enumerable.Range(0, 21).Select(i => new { label = $"C{i}", note = (string?)null }).ToArray();
        (await fixture.PutCombatant(campaign.Id, combat.Id, goblin.Id, Body(goblin, b => b["conditions"] = tooMany))).Status.Should().Be(400);
        (await fixture.PutCombatant(campaign.Id, combat.Id, goblin.Id, Body(goblin, b => b["conditions"] = new[] { new { label = new string('x', 41) } }))).Status.Should().Be(400);
        (await fixture.PutCombatant(campaign.Id, combat.Id, goblin.Id, Body(goblin, b => b["conditions"] = new[] { new { label = "Cursed", note = new string('x', 201) } }))).Status.Should().Be(400);
    }

    [Fact]
    public async Task APlayer_EditsTheirOwnHpConditionsAndWaitingInitiative_AndNothingElse()
    {
        var (campaign, combat) = await Setup("Combatants player edit");
        var brynn = await fixture.PlayerCharacter(campaign);
        combat = await Started(campaign, combat);
        fixture.LoginAsUser(Users.Player);
        var mine = (await fixture.AddCombatants(campaign.Id, combat.Id, new { entryId = brynn.Id })).Combat.Combatants.Single();

        var edited = await fixture.PutCombatant(campaign.Id, combat.Id, mine.Id, Body(mine, b =>
        {
            b["hp"] = 10;
            b["maxHp"] = 26;
            b["initiative"] = 17;
            b["conditions"] = new[] { new { label = "Blessed" } };
        }));
        edited.Status.Should().Be(200, edited.Body);
        mine = edited.Combat.Combatants.Single();
        (mine.Hp, mine.MaxHp, mine.Initiative).Should().Be(((int?)10, (int?)26, (int?)17));

        (await fixture.PutCombatant(campaign.Id, combat.Id, mine.Id, Body(mine, b => b["initiative"] = 20))).Status.Should().Be(403, "a placed initiative is the DM's");
        (await fixture.PutCombatant(campaign.Id, combat.Id, mine.Id, Body(mine, b => b["name"] = "Brynn the Bold"))).Status.Should().Be(403);
        (await fixture.PutCombatant(campaign.Id, combat.Id, mine.Id, Body(mine, b => b["ac"] = 30))).Status.Should().Be(403);
        (await fixture.PutCombatant(campaign.Id, combat.Id, mine.Id, Body(mine, b => b["hidden"] = true))).Status.Should().Be(403);
        (await fixture.PutCombatant(campaign.Id, combat.Id, mine.Id, Body(mine, b => b["playersSee"] = "Nothing"))).Status.Should().Be(403);
    }

    [Fact]
    public async Task APlayer_CannotEditOrRemoveAnotherCombatant_AndAHiddenOneIsA404()
    {
        var (campaign, combat) = await Setup("Combatants player others edit");
        var added = await fixture.AddAsDm(campaign.Id, combat.Id, new { name = "Goblin" }, new { name = "Lurker", hidden = true });
        combat = await Started(campaign, combat);
        var goblin = added.Combatants.Single(c => c.Name == "Goblin");
        var lurker = added.Combatants.Single(c => c.Name == "Lurker");

        fixture.LoginAsUser(Users.Player);
        var seen = (await fixture.GetCombat(campaign.Id, combat.Id)).Combat.Combatants.Single();
        (await fixture.PutCombatant(campaign.Id, combat.Id, goblin.Id, Body(seen, b => b["conditions"] = new[] { new { label = "Prone" } }))).Status.Should().Be(403);
        (await fixture.DeleteCombatant(campaign.Id, combat.Id, goblin.Id)).Status.Should().Be(403);
        (await fixture.PutCombatant(campaign.Id, combat.Id, lurker.Id, Body(seen))).Status.Should().Be(404);
        (await fixture.DeleteCombatant(campaign.Id, combat.Id, lurker.Id)).Status.Should().Be(404);
    }

    // Remove.

    [Fact]
    public async Task ADm_RemovesAny_AndAPlayerTheirOwn()
    {
        var (campaign, combat) = await Setup("Combatants remove");
        var brynn = await fixture.PlayerCharacter(campaign);
        var goblin = (await fixture.AddAsDm(campaign.Id, combat.Id, new { name = "Goblin" })).Combatants.Single();
        combat = await Started(campaign, combat);
        fixture.LoginAsUser(Users.Player);
        var mine = (await fixture.AddCombatants(campaign.Id, combat.Id, new { entryId = brynn.Id })).Combat
            .Combatants.Single(c => c.Name == "Brynn");

        var afterMine = await fixture.DeleteCombatant(campaign.Id, combat.Id, mine.Id);
        afterMine.Status.Should().Be(200);
        afterMine.Combat.Combatants.Select(c => c.Name).Should().Equal("Goblin");

        fixture.LoginAsUser(Users.DM);
        (await fixture.DeleteCombatant(campaign.Id, combat.Id, goblin.Id)).Combat.Combatants.Should().BeEmpty();
        (await fixture.DeleteCombatant(campaign.Id, combat.Id, goblin.Id)).Status.Should().Be(404);

        var events = await TestCampaign.EventsOf(fixture, combat.Id);
        events.Select(e => e.Data).OfType<CombatantRemoved>().Select(e => e.Actor.MemberId)
            .Should().Equal(campaign.PlayerMemberId, campaign.DmMemberId);
    }

    [Fact]
    public async Task RemovingTheTurnsCombatant_PassesTheTurnOn()
    {
        var (campaign, combat) = await Setup("Combatants remove turn");
        var added = await fixture.AddAsDm(campaign.Id, combat.Id, new { name = "A" }, new { name = "B" }, new { name = "C" });
        var ids = added.Combatants.ToDictionary(c => c.Name, c => c.Id);
        await fixture.Start(combat.Id, new Dictionary<Guid, int> { [ids["A"]] = 20, [ids["B"]] = 10, [ids["C"]] = 5 });

        fixture.LoginAsUser(Users.DM);
        (await fixture.GetCombat(campaign.Id, combat.Id)).Combat.TurnCombatantId.Should().Be(ids["A"]);
        var after = (await fixture.DeleteCombatant(campaign.Id, combat.Id, ids["A"])).Combat;
        (after.TurnCombatantId, after.Round).Should().Be(((Guid?)ids["B"], 1));
    }
}
