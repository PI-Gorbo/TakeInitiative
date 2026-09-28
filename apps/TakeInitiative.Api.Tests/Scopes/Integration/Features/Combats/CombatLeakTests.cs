using System.Text.Json;
using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Combats;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;

namespace TakeInitiative.Api.Tests.Integration.Features.Combats;

/// <summary>
/// The core of 18a (invariant 8): what must never reach a player's browser. Each test plants a
/// unique marker and reads, as <see cref="Users.Player"/>, <c>GET combats</c>,
/// <c>GET combats/{id}</c> and the <c>combatChanged</c> push to their member group, as raw JSON.
/// </summary>
public class CombatLeakTests : IClassFixture<RecordingHubFixture>
{
    private readonly RecordingHubFixture fixture;

    public CombatLeakTests(RecordingHubFixture fixture)
    {
        this.fixture = fixture;
        UseRealDice(fixture);
    }

    /// <summary>Everything a player got about the combat: both reads and every push to them since <paramref name="mark"/>.</summary>
    private async Task<string> WhatThePlayerGot(TestCampaign campaign, Guid combatId, int mark)
    {
        fixture.LoginAsUser(Users.Player);
        var list = await fixture.GetCombats(campaign.Id);
        list.Status.Should().Be(200);
        var one = await fixture.GetCombat(campaign.Id, combatId);
        var pushes = fixture.Hub.Messages.Skip(mark)
            .Where(m => m.Groups.Contains(CampaignGroups.Member(campaign.PlayerMemberId)) || m.Groups.Contains(CampaignGroups.Campaign(campaign.Id)))
            .Select(m => JsonSerializer.Serialize(m.Payload, Web));
        return string.Join("\n", [list.Body, one.Body, .. pushes]);
    }

    /// <summary>A DM edit with no change in it but a condition, to make the combat push again.</summary>
    private async Task Poke(TestCampaign campaign, Guid combatId, CombatantResponse visible)
    {
        fixture.LoginAsUser(Users.DM);
        var reply = await fixture.PutCombatant(campaign.Id, combatId, visible.Id,
            Body(visible, b => b["conditions"] = new[] { new { label = "Prone" } }));
        reply.Status.Should().Be(200, reply.Body);
    }

    [Fact]
    public async Task ADraftsName_NeverReachesAPlayer()
    {
        var campaign = await TestCampaign.Create(fixture, "Leak draft");
        var mark = fixture.Hub.Messages.Count;
        var combat = await fixture.CreateCombat(campaign.Id, "zanthor ambush");
        await fixture.AddAsDm(campaign.Id, combat.Id, new { name = "Goblin" });

        var got = await WhatThePlayerGot(campaign, combat.Id, mark);
        got.Should().NotContainEquivalentOf("zanthor");
        fixture.Hub.Messages.Skip(mark).Should().OnlyContain(m => m.Groups.SequenceEqual(new[] { CampaignGroups.Dms(campaign.Id) }),
            "a Draft goes to the DM group only");
    }

    [Fact]
    public async Task AHiddenCombatant_ItsNameHpAcInitiativeAndCount_NeverReachAPlayer()
    {
        var campaign = await TestCampaign.Create(fixture, "Leak hidden");
        var combat = await fixture.CreateCombat(campaign.Id);
        var added = await fixture.AddAsDm(campaign.Id, combat.Id,
            new { name = "Goblin" },
            new { name = "zanthor", hidden = true, maxHp = "4321", ac = 87, initiativeRoll = "1d20+77", playersSee = "Exact" });
        var goblin = added.Combatants.Single(c => c.Name == "Goblin");
        var zanthor = added.Combatants.Single(c => c.Name == "zanthor");
        await fixture.Start(combat.Id, new Dictionary<Guid, int> { [goblin.Id] = 3, [zanthor.Id] = -73 });
        var mark = fixture.Hub.Messages.Count;
        await Poke(campaign, combat.Id, goblin);

        var got = await WhatThePlayerGot(campaign, combat.Id, mark);
        got.Should().NotContainEquivalentOf("zanthor").And.NotContain("\":4321").And.NotContain("\":87").And.NotContain("\":-73")
            .And.NotContain(zanthor.Id.ToString());
        fixture.LoginAsUser(Users.Player);
        (await fixture.GetCombats(campaign.Id)).As<GetCombatsResponse>().Combats.Single().CombatantCount.Should().Be(1);
        (await fixture.GetCombat(campaign.Id, combat.Id)).Combat.Combatants.Should().ContainSingle();
    }

    [Fact]
    public async Task TheTurnOnAHiddenCombatant_IsNoTurnForAPlayer()
    {
        var campaign = await TestCampaign.Create(fixture, "Leak turn");
        var combat = await fixture.CreateCombat(campaign.Id);
        var added = await fixture.AddAsDm(campaign.Id, combat.Id, new { name = "Goblin" }, new { name = "zanthor", hidden = true });
        var goblin = added.Combatants.Single(c => c.Name == "Goblin");
        var zanthor = added.Combatants.Single(c => c.Name == "zanthor");
        await fixture.Start(combat.Id, new Dictionary<Guid, int> { [goblin.Id] = 3, [zanthor.Id] = 20 });
        var mark = fixture.Hub.Messages.Count;
        await Poke(campaign, combat.Id, goblin);

        fixture.LoginAsUser(Users.DM);
        (await fixture.GetCombat(campaign.Id, combat.Id)).Combat.TurnCombatantId.Should().Be(zanthor.Id);
        var got = await WhatThePlayerGot(campaign, combat.Id, mark);
        got.Should().NotContain(zanthor.Id.ToString());
        fixture.LoginAsUser(Users.Player);
        (await fixture.GetCombat(campaign.Id, combat.Id)).Combat.TurnCombatantId.Should().BeNull();
    }

    [Theory]
    [InlineData("Band")]
    [InlineData("Nothing")]
    public async Task AMonstersHpAndAc_NeverReachAPlayer(string playersSee)
    {
        var campaign = await TestCampaign.Create(fixture, $"Leak {playersSee}");
        var combat = await fixture.CreateCombat(campaign.Id);
        var added = await fixture.AddAsDm(campaign.Id, combat.Id,
            new { name = "Ogre", maxHp = "4321", ac = 87, initiativeRoll = "1d20+77", playersSee });
        var ogre = added.Combatants.Single();
        await fixture.Start(combat.Id, new Dictionary<Guid, int> { [ogre.Id] = 5 });
        var mark = fixture.Hub.Messages.Count;
        await Poke(campaign, combat.Id, ogre);

        var got = await WhatThePlayerGot(campaign, combat.Id, mark);
        got.Should().NotContain("\":4321").And.NotContain("\":87").And.NotContain("1d20+77")
            .And.NotContain("\"hp\"").And.NotContain("\"maxHp\"").And.NotContain("\"ac\"");
        if (playersSee == "Nothing")
        {
            got.Should().NotContain("\"band\"", "Nothing shows no band either");
        }
        else
        {
            got.Should().Contain("\"band\":\"Healthy\"");
        }
    }

    [Fact]
    public async Task ACombatantFromADmEntry_IsHiddenByDefault_AndItsEntryStaysUnlinkedWhenShown()
    {
        var campaign = await TestCampaign.Create(fixture, "Leak DM entry");
        var klarg = await fixture.Entry(Users.DM, campaign.Id, "zanthor the bugbear", Visibility.DM, stats: (null, "27", 16));
        var combat = await fixture.CreateCombat(campaign.Id);
        var added = await fixture.AddAsDm(campaign.Id, combat.Id, new { name = "Goblin" }, new { entryId = klarg.Id });
        var row = added.Combatants.Single(c => c.EntryId == klarg.Id);
        row.Hidden.Should().BeTrue("nothing is revealed automatically (invariant 5)");
        await fixture.Start(combat.Id, new Dictionary<Guid, int>());
        var mark = fixture.Hub.Messages.Count;
        await Poke(campaign, combat.Id, added.Combatants.Single(c => c.Name == "Goblin"));

        (await WhatThePlayerGot(campaign, combat.Id, mark)).Should().NotContainEquivalentOf("zanthor");

        // The DM reveals it: the name shows, as text, and the entry's id never does.
        fixture.LoginAsUser(Users.DM);
        (await fixture.PutCombatant(campaign.Id, combat.Id, row.Id, Body(row, b => b["hidden"] = false))).Status.Should().Be(200);
        fixture.LoginAsUser(Users.Player);
        var seen = (await fixture.GetCombat(campaign.Id, combat.Id)).Combat.Combatants.Single(c => c.Id == row.Id);
        seen.Name.Should().Be("zanthor the bugbear");
        seen.EntryId.Should().BeNull();
        var got = await WhatThePlayerGot(campaign, combat.Id, mark);
        got.Should().NotContain(klarg.Id.ToString()).And.NotContain("\"27\"").And.NotContain("\"hp\":27");
    }

    [Fact]
    public async Task AnotherCampaignsMember_GetsA404_AndNothingInTheirList()
    {
        var campaign = await TestCampaign.Create(fixture, "Leak other campaign");
        var combat = await fixture.CreateCombat(campaign.Id, "zanthor raid");
        await fixture.Start(combat.Id, new Dictionary<Guid, int>());

        fixture.LoginAsUser(Users.Stranger);
        var strangers = await fixture.PostCreateCampaign(new() { Name = "Stranger's own" });
        (await fixture.GetCombat(strangers.Value.Id, combat.Id)).Status.Should().Be(404);
        var list = await fixture.GetCombats(strangers.Value.Id);
        list.Body.Should().NotContainEquivalentOf("zanthor");
        (await fixture.GetCombat(campaign.Id, combat.Id)).Status.Should().Be(403, "not a member of the combat's campaign");
    }

    [Fact]
    public async Task NoPlayerResponse_CarriesATiebreak()
    {
        var campaign = await TestCampaign.Create(fixture, "Leak tiebreak");
        var brynn = await fixture.PlayerCharacter(campaign);
        var combat = await fixture.CreateCombat(campaign.Id);
        var added = await fixture.AddAsDm(campaign.Id, combat.Id, new { name = "Goblin", count = 3 });
        await fixture.Start(combat.Id, added.Combatants.ToDictionary(c => c.Id, _ => 10));
        var mark = fixture.Hub.Messages.Count;

        fixture.LoginAsUser(Users.Player);
        var mine = await fixture.AddCombatants(campaign.Id, combat.Id, new { entryId = brynn.Id });
        var edited = await fixture.PutCombatant(campaign.Id, combat.Id, mine.Combat.Combatants.Single(c => c.Name == "Brynn").Id,
            Body(mine.Combat.Combatants.Single(c => c.Name == "Brynn"), b => b["hp"] = 20));

        var got = string.Join("\n", mine.Body, edited.Body, await WhatThePlayerGot(campaign, combat.Id, mark));
        got.Should().NotContainEquivalentOf("tiebreak");
    }
}
