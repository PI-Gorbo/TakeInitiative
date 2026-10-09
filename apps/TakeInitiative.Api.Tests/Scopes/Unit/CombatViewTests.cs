using System.Text.Json;
using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Combats;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>
/// Redaction (18a.5, invariant 8): for each PlayersSee × owner or not × hidden or not × DM or
/// player, exactly the fields a viewer gets. And <c>Tiebreak</c> is never serialised.
/// </summary>
public class CombatViewTests
{
    private static readonly Member Dm = new() { MemberId = Guid.NewGuid(), UserId = Guid.NewGuid(), Role = Role.DM, JoinedAt = default };
    private static readonly Member Player = new() { MemberId = Guid.NewGuid(), UserId = Guid.NewGuid(), Role = Role.Player, JoinedAt = default };
    private static readonly Member OtherPlayer = new() { MemberId = Guid.NewGuid(), UserId = Guid.NewGuid(), Role = Role.Player, JoinedAt = default };
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static Combatant Goblin(PlayersSee playersSee, bool hidden, Guid? owner, Guid? entryId = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Goblin 1",
        EntryId = entryId,
        OwnerMemberId = owner,
        InitiativeRoll = "1d20+2",
        Initiative = 14,
        Tiebreak = 123456,
        Hp = 7,
        MaxHp = 15,
        Ac = 13,
        Hidden = hidden,
        PlayersSee = playersSee,
        Conditions = [new Condition("Poisoned", "until dawn")],
    };

    private static Combat With(params Combatant[] combatants) => new()
    {
        Id = Guid.NewGuid(),
        CampaignId = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Name = "Ambush",
        Status = CombatStatus.Active,
        Round = 1,
        TurnCombatantId = combatants.FirstOrDefault()?.Id,
        Combatants = combatants,
        StartedAt = DateTimeOffset.UnixEpoch,
    };

    public static TheoryData<PlayersSee, bool, bool, bool> Matrix()
    {
        var data = new TheoryData<PlayersSee, bool, bool, bool>();
        foreach (var playersSee in Enum.GetValues<PlayersSee>())
        foreach (var owned in new[] { false, true })
        foreach (var hidden in new[] { false, true })
        foreach (var dm in new[] { false, true })
        {
            data.Add(playersSee, owned, hidden, dm);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void EachViewerGetsExactlyTheirFields(PlayersSee playersSee, bool ownedByViewer, bool hidden, bool viewerIsDm)
    {
        var viewer = viewerIsDm ? Dm : Player;
        var goblin = Goblin(playersSee, hidden, ownedByViewer ? viewer.MemberId : OtherPlayer.MemberId);
        var view = CombatView.For(With(goblin), viewer, new Dictionary<Guid, Guid>());

        var exact = viewerIsDm || ownedByViewer;
        if (hidden && !exact)
        {
            view.Combatants.Should().BeEmpty("a hidden combatant is not a row, a name or a count");
            view.TurnCombatantId.Should().BeNull("the turn on a hidden combatant is no turn");
            CombatView.Summary(With(goblin), viewer, 1).CombatantCount.Should().Be(0);
            return;
        }

        var row = view.Combatants.Should().ContainSingle().Subject;
        view.TurnCombatantId.Should().Be(goblin.Id);
        row.Name.Should().Be("Goblin 1");
        row.Initiative.Should().Be(14);
        row.Waiting.Should().BeFalse();
        row.Conditions.Should().Equal(new Condition("Poisoned", "until dawn"));
        row.PlayersSee.Should().Be(playersSee);
        row.Hidden.Should().Be(exact && hidden);
        row.InitiativeRoll.Should().Be(exact ? "1d20+2" : null);

        var showHp = exact || playersSee == PlayersSee.Exact;
        row.Hp.Should().Be(showHp ? 7 : null);
        row.MaxHp.Should().Be(showHp ? 15 : null);
        row.Ac.Should().Be(showHp ? 13 : null);
        row.Band.Should().Be(showHp || playersSee == PlayersSee.Band ? HpBand.Bloodied : null);
    }

    [Theory]
    [InlineData(15, 15, HpBand.Healthy)]
    [InlineData(8, 15, HpBand.Healthy)]
    [InlineData(7, 15, HpBand.Bloodied)]
    [InlineData(1, 15, HpBand.Bloodied)]
    [InlineData(0, 15, HpBand.Down)]
    [InlineData(-5, 15, HpBand.Down)]
    public void Bands_FollowTheNotes(int hp, int maxHp, HpBand band)
        => HpBands.Of(hp, maxHp).Should().Be(band);

    [Fact]
    public void NoMaxHp_IsNoBand()
        => HpBands.Of(5, null).Should().BeNull();

    [Fact]
    public void EntryIds_AreOnlyTheOnesTheViewerCanSee_Resolved()
    {
        var visible = Guid.NewGuid();
        var merged = Guid.NewGuid();
        var mergedInto = Guid.NewGuid();
        var secret = Guid.NewGuid();
        var combat = With(
            Goblin(PlayersSee.Band, false, null, visible),
            Goblin(PlayersSee.Band, false, null, merged),
            Goblin(PlayersSee.Band, false, null, secret));

        var view = CombatView.For(combat, Player, new Dictionary<Guid, Guid> { [visible] = visible, [merged] = mergedInto });

        view.Combatants.Select(c => c.EntryId).Should().BeEquivalentTo(new Guid?[] { visible, mergedInto, null });
    }

    [Fact]
    public void Tiebreak_IsNeverSerialised_AndRedactedFieldsAreAbsent()
    {
        var combat = With(
            Goblin(PlayersSee.Band, false, null),
            Goblin(PlayersSee.Nothing, false, null),
            Goblin(PlayersSee.Exact, true, null));

        foreach (var viewer in new[] { Dm, Player })
        {
            var json = JsonSerializer.Serialize(CombatView.For(combat, viewer, new Dictionary<Guid, Guid>()), Web);
            json.Should().NotContainEquivalentOf("tiebreak");
            json.Should().NotContain("123456");
        }

        var player = JsonSerializer.Serialize(CombatView.For(combat, Player, new Dictionary<Guid, Guid>()), Web);
        player.Should().NotContain("\"hp\"").And.NotContain("\"maxHp\"").And.NotContain("\"ac\"");
        player.Should().NotContain("initiativeRoll");
        using var doc = JsonDocument.Parse(player);
        doc.RootElement.GetProperty("combatants").GetArrayLength().Should().Be(2);
    }
}
