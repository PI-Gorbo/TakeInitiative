using System.Collections.Immutable;
using FluentAssertions;
using NSubstitute;
using NSubstitute.Core;
using TakeInitiative.Api.Features;
using TakeInitiative.Api.Features.Combats;
using TakeInitiative.Utilities;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("DynamicProxyGenAssembly2")]
namespace TakeInitiative.Api.Tests.Unit;

public class DiceRollerTests
{
    private IDiceRoller diceRoller;
    private InitiativeRoller initiativeRoller;

    public DiceRollerTests()
    {
        this.diceRoller = Substitute.For<IDiceRoller>();
        this.initiativeRoller = new InitiativeRoller(diceRoller);
    }

    [Fact]
    public void MergesRollsSuccessfully_Situation2()
    {

        Guid playerId = Guid.NewGuid();
        var incomingChar = CombatCharacter.NewCombatCharacter(
                    Id: Guid.NewGuid(),
                    playerId,
                    "incoming",
                    new CharacterInitiative()
                    {
                        Strategy = InitiativeStrategy.Roll,
                        Value = "1d20 + 5"
                    },
                    null,
                    null,
                    false,
                    CharacterOriginDetails.CustomCharacter(),
                    null
                );

        var existingChar = CombatCharacter.NewCombatCharacter(
                    Id: Guid.NewGuid(),
                    playerId,
                    "Char1",
                    new CharacterInitiative()
                    {
                        Strategy = InitiativeStrategy.Roll,
                        Value = "1d20 + 5"
                    },
                    null,
                    null,
                    false,
                    CharacterOriginDetails.CustomCharacter(),
                    null
                );
        existingChar.InitiativeValue = [6];

        this.diceRoller.EvaluateRoll("1d20 + 5").Returns(6);
        int callCount = 1;
        this.diceRoller.RollD20().Returns((CallInfo callInfo) =>
        {
            return callCount++;
        });
        var result = this.initiativeRoller.ComputeRolls(
            [incomingChar],
            [existingChar]
        );
        result.Should().Succeed();
        var existingCharAfterRoll = result.Value.Find(x => x.id == existingChar.Id);
        existingCharAfterRoll.Should().NotBeNull();
        existingCharAfterRoll.rolls.SequenceEqual([6, 1]).Should().BeTrue();
        var newCharAfterRoll = result.Value.Find(x => x.id == incomingChar.Id);
        newCharAfterRoll.Should().NotBeNull();
        newCharAfterRoll.rolls.SequenceEqual([6, 2]).Should().BeTrue();
    }

    [Fact]
    public void MergesRollsSuccessfully_NoCollide()
    {
        Guid playerId = Guid.NewGuid();
        var incomingChar = CombatCharacter.NewCombatCharacter(
                    Id: Guid.NewGuid(),
                    playerId,
                    "Incoming",
                    new CharacterInitiative()
                    {
                        Strategy = InitiativeStrategy.Roll,
                        Value = "1d20 + 5"
                    },
                    null,
                    null,
                    false,
                    CharacterOriginDetails.CustomCharacter(),
                    null
                );

        var existingChar1 = CombatCharacter.NewCombatCharacter(
                    Id: Guid.NewGuid(),
                    playerId,
                    "Char1",
                    new CharacterInitiative()
                    {
                        Strategy = InitiativeStrategy.Roll,
                        Value = "1d20 + 5"
                    },
                    null,
                    null,
                    false,
                    CharacterOriginDetails.CustomCharacter(),
                    null
                );
        existingChar1.InitiativeValue = [6, 2];

        var existingChar2 = CombatCharacter.NewCombatCharacter(
                    Id: Guid.NewGuid(),
                    playerId,
                    "Char2",
                    new CharacterInitiative()
                    {
                        Strategy = InitiativeStrategy.Roll,
                        Value = "1d20 + 5"
                    },
                    null,
                    null,
                    false,
                    CharacterOriginDetails.CustomCharacter(),
                    null
                );
        existingChar2.InitiativeValue = [6, 5];

        this.diceRoller.EvaluateRoll("1d20 + 5").Returns(6);
        int callCount = 1;
        this.diceRoller.RollD20().Returns((CallInfo callInfo) =>
        {
            return callCount++;
        });
        var result = this.initiativeRoller.ComputeRolls(
            [incomingChar],
            [existingChar1, existingChar2]
        );
        result.Should().Succeed();

        var existingChar1AfterRoll = result.Value.Find(x => x.id == existingChar1.Id);
        existingChar1AfterRoll.Should().NotBeNull();
        existingChar1AfterRoll.rolls.SequenceEqual([6, 2]).Should().BeTrue();

        var existingChar2AfterRoll = result.Value.Find(x => x.id == existingChar2.Id);
        existingChar2AfterRoll.Should().NotBeNull();
        existingChar2AfterRoll.rolls.SequenceEqual([6, 5]).Should().BeTrue();

        var newCharAfterRoll = result.Value.Find(x => x.id == incomingChar.Id);
        newCharAfterRoll.Should().NotBeNull();
        newCharAfterRoll.rolls.SequenceEqual([6, 1]).Should().BeTrue();
    }

    [Fact]
    public void MergesRollsSuccessfully_CollideOnMerge()
    {
        Guid playerId = Guid.NewGuid();
        var incomingChar = CombatCharacter.NewCombatCharacter(
                    Id: Guid.NewGuid(),
                    playerId,
                    "Incoming",
                    new CharacterInitiative()
                    {
                        Strategy = InitiativeStrategy.Roll,
                        Value = "1d20 + 3"
                    },
                    null,
                    null,
                    false,
                    CharacterOriginDetails.CustomCharacter(),
                    null
                );

        var existingChar1 = CombatCharacter.NewCombatCharacter(
                    Id: Guid.NewGuid(),
                    playerId,
                    "Char1",
                    new CharacterInitiative()
                    {
                        Strategy = InitiativeStrategy.Roll,
                        Value = "1d20 + 4"
                    },
                    null,
                    null,
                    false,
                    CharacterOriginDetails.CustomCharacter(),
                    null
                );
        existingChar1.InitiativeValue = [6, 2];

        var existingChar2 = CombatCharacter.NewCombatCharacter(
                    Id: Guid.NewGuid(),
                    playerId,
                    "Char2",
                    new CharacterInitiative()
                    {
                        Strategy = InitiativeStrategy.Roll,
                        Value = "1d20 + 5"
                    },
                    null,
                    null,
                    false,
                    CharacterOriginDetails.CustomCharacter(),
                    null
                );
        existingChar2.InitiativeValue = [6, 5];

        this.diceRoller.EvaluateRoll("1d20 + 3").Returns(6);
        int callCount = 5;
        this.diceRoller.RollD20().Returns((CallInfo callInfo) =>
        {
            return callCount++;
        });
        var result = this.initiativeRoller.ComputeRolls(
            [incomingChar],
            [existingChar1, existingChar2]
        );
        result.Should().Succeed();

        var existingChar1AfterRoll = result.Value.Find(x => x.id == existingChar1.Id);
        existingChar1AfterRoll.Should().NotBeNull();
        existingChar1AfterRoll.rolls.SequenceEqual([6, 2]).Should().BeTrue();

        var existingChar2AfterRoll = result.Value.Find(x => x.id == existingChar2.Id);
        existingChar2AfterRoll.Should().NotBeNull();
        existingChar2AfterRoll.rolls.SequenceEqual([6, 5, 6]).Should().BeTrue();

        var newCharAfterRoll = result.Value.Find(x => x.id == incomingChar.Id);
        newCharAfterRoll.Should().NotBeNull();
        newCharAfterRoll.rolls.SequenceEqual([6, 5, 7]).Should().BeTrue();
    }
}
