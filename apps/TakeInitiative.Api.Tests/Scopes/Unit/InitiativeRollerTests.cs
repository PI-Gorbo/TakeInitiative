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
        diceRoller = Substitute.For<IDiceRoller>();
        initiativeRoller = new InitiativeRoller(diceRoller);
    }

    [Fact]
    public void MergesRollsSuccessfully_Situation2()
    {

        Guid playerId = Guid.NewGuid();
        var incomingChar = new StagedCharacter(
                    Id: Guid.NewGuid(),
                    PlayerId: playerId,
                    Name: "incoming",
                    Initiative: new CharacterInitiative()
                    {
                        Strategy = InitiativeStrategy.Roll,
                        Value = "1d20 + 5"
                    },
                    Health: null,
                    ArmourClass: null,
                    Hidden: false,
                    CharacterOriginDetails: CharacterOriginDetails.CustomCharacter(),
                    CopyNumber: null
                );

        var existingChar = new InitiativeCharacter(
                    Id: Guid.NewGuid(),
                    PlayerId: playerId,
                    Name: "Char1",
                    Initiative: new CharacterInitiative()
                    {
                        Strategy = InitiativeStrategy.Roll,
                        Value = "1d20 + 5"
                    },
                    Health: null,
                    ArmourClass: null,
                    Hidden: false,
                    CharacterOriginDetails: CharacterOriginDetails.CustomCharacter(),
                    CopyNumber: null,
                    InitiativeValue: [6],
                    Conditions: []
                );

        diceRoller.EvaluateRoll("1d20 + 5").Returns(6);
        int callCount = 1;
        diceRoller.RollD20().Returns((CallInfo callInfo) =>
        {
            return callCount++;
        });
        var result = initiativeRoller.ComputeRolls(
            [incomingChar],
            [existingChar]
        );
        result.Should().Succeed();
        var existingCharAfterRoll = result.Value.Find(x => x.id == existingChar.Id);
        existingCharAfterRoll.Should().NotBeNull();
        existingCharAfterRoll!.rolls.SequenceEqual([6, 1]).Should().BeTrue();
        var newCharAfterRoll = result.Value.Find(x => x.id == incomingChar.Id);
        newCharAfterRoll.Should().NotBeNull();
        newCharAfterRoll!.rolls.SequenceEqual([6, 2]).Should().BeTrue();
    }

    [Fact]
    public void MergesRollsSuccessfully_NoCollide()
    {
        Guid playerId = Guid.NewGuid();
        var incomingChar = new StagedCharacter(
            Id: Guid.NewGuid(),
            PlayerId: playerId,
            Name: "Incoming",
            Health: null,
            Initiative: new CharacterInitiative()
            {
                Strategy = InitiativeStrategy.Roll,
                Value = "1d20 + 5"
            },
            ArmourClass: null,
            CharacterOriginDetails: CharacterOriginDetails.CustomCharacter(),
            Hidden: false,
            CopyNumber: null);

        var existingChar1 = new InitiativeCharacter(
                    Id: Guid.NewGuid(),
                    PlayerId: playerId,
                    Name: "Char1",
                    Initiative: new CharacterInitiative()
                    {
                        Strategy = InitiativeStrategy.Roll,
                        Value = "1d20 + 5"
                    },
                    Health: null,
                    ArmourClass: null,
                    Hidden: false,
                    CharacterOriginDetails: CharacterOriginDetails.CustomCharacter(),
                    CopyNumber: null,
                    InitiativeValue: [6, 2],
                    Conditions: []
                );

        var existingChar2 = new InitiativeCharacter(
                    Id: Guid.NewGuid(),
                    PlayerId: playerId,
                    Name: "Char2",
                    Initiative: new CharacterInitiative()
                    {
                        Strategy = InitiativeStrategy.Roll,
                        Value = "1d20 + 5"
                    },
                    Health: null,
                    ArmourClass: null,
                    Hidden: false,
                    CharacterOriginDetails: CharacterOriginDetails.CustomCharacter(),
                    CopyNumber: null,
                    InitiativeValue: [6, 5],
                    Conditions: []
                );

        diceRoller.EvaluateRoll("1d20 + 5").Returns(6);
        int callCount = 1;
        diceRoller.RollD20().Returns((CallInfo callInfo) =>
        {
            return callCount++;
        });
        var result = initiativeRoller.ComputeRolls(
            [incomingChar],
            [existingChar1, existingChar2]
        );
        result.Should().Succeed();

        var existingChar1AfterRoll = result.Value.Find(x => x.id == existingChar1.Id);
        existingChar1AfterRoll.Should().NotBeNull();
        existingChar1AfterRoll!.rolls.SequenceEqual([6, 2]).Should().BeTrue();

        var existingChar2AfterRoll = result.Value.Find(x => x.id == existingChar2.Id);
        existingChar2AfterRoll.Should().NotBeNull();
        existingChar2AfterRoll!.rolls.SequenceEqual([6, 5]).Should().BeTrue();

        var newCharAfterRoll = result.Value.Find(x => x.id == incomingChar.Id);
        newCharAfterRoll.Should().NotBeNull();
        newCharAfterRoll!.rolls.SequenceEqual([6, 1]).Should().BeTrue();
    }

    [Fact]
    public void MergesRollsSuccessfully_CollideOnMerge()
    {
        Guid playerId = Guid.NewGuid();
        var incomingChar = new StagedCharacter(
                    Id: Guid.NewGuid(),
                    PlayerId: playerId,
                    Name: "Incoming",
                    Initiative: new CharacterInitiative()
                    {
                        Strategy = InitiativeStrategy.Roll,
                        Value = "1d20 + 3"
                    },
                    Health: null,
                    ArmourClass: null,
                    Hidden: false,
                    CharacterOriginDetails: CharacterOriginDetails.CustomCharacter(),
                    CopyNumber: null
                );

        var existingChar1 = new InitiativeCharacter(
                    Id: Guid.NewGuid(),
                    PlayerId: playerId,
                    Name: "Char1",
                    Initiative: new CharacterInitiative()
                    {
                        Strategy = InitiativeStrategy.Roll,
                        Value = "1d20 + 4"
                    },
                    Health: null,
                    ArmourClass: null,
                    Hidden: false,
                    CharacterOriginDetails: CharacterOriginDetails.CustomCharacter(),
                    CopyNumber: null,
                    InitiativeValue: [6, 2],
                    Conditions: []
                );

        var existingChar2 = new InitiativeCharacter(
                    Id: Guid.NewGuid(),
                    PlayerId: playerId,
                    Name: "Char2",
                    Initiative: new CharacterInitiative()
                    {
                        Strategy = InitiativeStrategy.Roll,
                        Value = "1d20 + 5"
                    },
                    Health: null,
                    ArmourClass: null,
                    Hidden: false,
                    CharacterOriginDetails: CharacterOriginDetails.CustomCharacter(),
                    CopyNumber: null,
                    InitiativeValue: [6, 5],
                    Conditions: []
                );

        diceRoller.EvaluateRoll("1d20 + 3").Returns(6);
        int callCount = 5;
        diceRoller.RollD20().Returns((CallInfo callInfo) =>
        {
            return callCount++;
        });
        var result = initiativeRoller.ComputeRolls(
            [incomingChar],
            [existingChar1, existingChar2]
        );
        result.Should().Succeed();

        var existingChar1AfterRoll = result.Value.Find(x => x.id == existingChar1.Id);
        existingChar1AfterRoll.Should().NotBeNull();
        existingChar1AfterRoll!.rolls.SequenceEqual([6, 2]).Should().BeTrue();

        var existingChar2AfterRoll = result.Value.Find(x => x.id == existingChar2.Id);
        existingChar2AfterRoll.Should().NotBeNull();
        existingChar2AfterRoll!.rolls.SequenceEqual([6, 5, 6]).Should().BeTrue();

        var newCharAfterRoll = result.Value.Find(x => x.id == incomingChar.Id);
        newCharAfterRoll.Should().NotBeNull();
        newCharAfterRoll!.rolls.SequenceEqual([6, 5, 7]).Should().BeTrue();
    }
}
