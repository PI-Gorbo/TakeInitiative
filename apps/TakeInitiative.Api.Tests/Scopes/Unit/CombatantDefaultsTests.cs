using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Combats;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>
/// Defaults from an entry (18a.3): numbering, HP rolled per copy, the PlayersSee and Hidden
/// defaults, and no stats for a player.
/// </summary>
public class CombatantDefaultsTests
{
    private static readonly Member Dm = new() { MemberId = Guid.NewGuid(), UserId = Guid.NewGuid(), Role = Role.DM, JoinedAt = default };
    private static readonly Member Player = new() { MemberId = Guid.NewGuid(), UserId = Guid.NewGuid(), Role = Role.Player, JoinedAt = default };

    private static Entry Character(string name, Stats? stats, Visibility visibility = Visibility.Everyone, Guid? claimer = null) => new()
    {
        Id = Guid.NewGuid(),
        CampaignId = Guid.NewGuid(),
        CreatorMemberId = Dm.MemberId,
        Name = name,
        Kind = EntryKind.Character,
        Visibility = visibility,
        ClaimedByMemberId = claimer,
        Stats = stats,
    };

    private static readonly Stats GoblinStats = new() { InitiativeRoll = "1d20+2", MaxHp = "2d6", Ac = 15 };

    private static IReadOnlyList<Combatant> Build(Member caller, IEnumerable<Combatant> existing, params CombatantPick[] picks)
    {
        var built = CombatantDefaults.Build(picks, caller, existing, new DiceRoller(new Random(42)), new Random(1));
        built.IsSuccess.Should().BeTrue(built.IsFailure ? built.Error : "");
        return built.Value;
    }

    [Theory]
    [InlineData(4, new string[0], new[] { "Goblin 1", "Goblin 2", "Goblin 3", "Goblin 4" })]
    [InlineData(1, new string[0], new[] { "Goblin" })]
    [InlineData(1, new[] { "Goblin 1", "Goblin 2", "Goblin 3", "Goblin 4" }, new[] { "Goblin 5" })]
    [InlineData(2, new[] { "goblin 3" }, new[] { "Goblin 4", "Goblin 5" })]
    [InlineData(1, new[] { "Goblin" }, new[] { "Goblin 2" })]
    [InlineData(1, new[] { "Goblin King" }, new[] { "Goblin" })]
    public void Names_AreNumbered(int count, string[] taken, string[] expected)
        => CombatantDefaults.Names("Goblin", count, taken).Should().Equal(expected);

    [Fact]
    public void Names_ContinueWithinOneRequest()
    {
        var goblin = Character("Goblin", GoblinStats);
        var added = Build(Dm, [], new CombatantPick { Entry = goblin, Count = 2 }, new CombatantPick { Entry = goblin, Count = 1 });
        added.Select(c => c.Name).Should().Equal("Goblin 1", "Goblin 2", "Goblin 3");
    }

    [Fact]
    public void MaxHp_IsRolledPerCopy_AndHpStartsAtIt()
    {
        var goblin = Character("Goblin", new Stats { MaxHp = "10d100" });
        var added = Build(Dm, [], new CombatantPick { Entry = goblin, Count = 6 });

        added.Should().OnlyContain(c => c.Hp == c.MaxHp && c.MaxHp >= 10 && c.MaxHp <= 1000);
        added.Select(c => c.MaxHp).Distinct().Should().HaveCountGreaterThan(1, "each copy rolls its own HP");
        added.Select(c => c.Tiebreak).Distinct().Should().HaveCount(6);
    }

    [Fact]
    public void AnUnclaimedEntry_ForADm_TakesItsStats_AndIsBand()
    {
        var goblin = Character("Goblin", GoblinStats);
        var c = Build(Dm, [], new CombatantPick { Entry = goblin }).Single();

        c.EntryId.Should().Be(goblin.Id);
        c.Ac.Should().Be(15);
        c.InitiativeRoll.Should().Be("1d20+2");
        c.MaxHp.Should().BeInRange(2, 12);
        c.OwnerMemberId.Should().BeNull();
        c.PlayersSee.Should().Be(PlayersSee.Band);
        c.Hidden.Should().BeFalse();
        c.IsWaiting.Should().BeTrue();
    }

    [Fact]
    public void APlayer_NeverReadsAnNpcsStats()
    {
        var goblin = Character("Goblin", GoblinStats);
        var c = Build(Player, [], new CombatantPick { Entry = goblin }).Single();

        c.Ac.Should().BeNull();
        c.MaxHp.Should().BeNull();
        c.Hp.Should().BeNull();
        c.InitiativeRoll.Should().Be("1d20");
    }

    [Fact]
    public void AClaimedCharacter_IsOwnedAndExact()
    {
        var brynn = Character("Brynn", new Stats { MaxHp = "24", Ac = 15, InitiativeRoll = "1d20+3" }, claimer: Player.MemberId);
        var c = Build(Player, [], new CombatantPick { Entry = brynn }).Single();

        c.OwnerMemberId.Should().Be(Player.MemberId);
        c.PlayersSee.Should().Be(PlayersSee.Exact);
        (c.Hp, c.MaxHp, c.Ac, c.InitiativeRoll).Should().Be((24, 24, 15, "1d20+3"));
    }

    [Theory]
    [InlineData(Visibility.Everyone, false)]
    [InlineData(Visibility.DM, true)]
    [InlineData(Visibility.Me, true)]
    public void AnEntryNotForEveryone_StartsHidden(Visibility visibility, bool hidden)
    {
        var klarg = Character("Klarg", null, visibility);
        Build(Dm, [], new CombatantPick { Entry = klarg }).Single().Hidden.Should().Be(hidden);
    }

    [Fact]
    public void WhatTheRequestSets_Wins()
    {
        var goblin = Character("Goblin", GoblinStats, Visibility.DM);
        var c = Build(Dm, [], new CombatantPick
        {
            Entry = goblin,
            Name = "Boss",
            InitiativeRoll = "1d20+9",
            MaxHp = "40",
            Ac = 18,
            Hidden = false,
            PlayersSee = PlayersSee.Nothing,
        }).Single();

        (c.Name, c.InitiativeRoll, c.MaxHp, c.Ac, c.Hidden, c.PlayersSee).Should().Be(("Boss", "1d20+9", 40, 18, false, PlayersSee.Nothing));
    }

    [Fact]
    public void APlainName_TakesTheRequest_WithA1d20Roll()
    {
        var c = Build(Dm, [], new CombatantPick { Name = "  Bandit  ", MaxHp = "11" }).Single();

        (c.Name, c.EntryId, c.InitiativeRoll, c.Hp, c.PlayersSee, c.Hidden).Should().Be(("Bandit", (Guid?)null, "1d20", 11, PlayersSee.Band, false));
    }

    [Fact]
    public void RolledMaxHp_IsAtLeastOne()
    {
        var c = Build(Dm, [], new CombatantPick { Name = "Rat", MaxHp = "1d4-10" }).Single();
        c.MaxHp.Should().Be(1);
    }
}
