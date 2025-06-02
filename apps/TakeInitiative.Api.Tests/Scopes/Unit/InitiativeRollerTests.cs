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
			Initiative: new UnevaluatedCharacterInitiative("1d20 + 5"),
			Health: new UnevaluatedCharacterHealth.None(),
			ArmourClass: null,
			Hidden: false,
			CharacterOriginDetails: CharacterOriginDetails.CustomCharacter(),
			CopyNumber: null
		);

		var existingChar = new InitiativeCharacter(
			Id: Guid.NewGuid(),
			PlayerId: playerId,
			Name: "Char1",
			Initiative: new CharacterInitiative([new(6, "1d20 + 1", "1d20(5) + 1 = 6")]),
			Health: new CharacterHealth.None(),
			ArmourClass: null,
			Hidden: false,
			CharacterOriginDetails: CharacterOriginDetails.CustomCharacter(),
			CopyNumber: null,
			Conditions: []
		);

		diceRoller.EvaluateRoll("1d20 + 5").Returns(new DiceRoll(6, "1d20 + 5", "1d20(1) + 5 = 6"));
		int callCount = 1;
		diceRoller.RollD20().Returns((CallInfo callInfo) =>
		{
			return new DiceRoll(callCount, "1d20", $"1d20({callCount}) = {callCount++}");
		});
		var result = initiativeRoller.ComputeRolls(
			[incomingChar],
			[existingChar]
		);
		result.Should().Succeed();
		var existingCharAfterRoll = result.Value[existingChar.Id];
		existingCharAfterRoll.Should().NotBeNull();
		existingCharAfterRoll.Value.Select(x => x.Total).SequenceEqual([6, 1]).Should().BeTrue();
		var newCharAfterRoll = result.Value[incomingChar.Id];
		newCharAfterRoll.Should().NotBeNull();
		newCharAfterRoll!.Value.Select(x => x.Total).SequenceEqual([6, 2]).Should().BeTrue();
	}

	[Fact]
	public void MergesRollsSuccessfully_NoCollide()
	{
		Guid playerId = Guid.NewGuid();
		var incomingChar = new StagedCharacter(
			Id: Guid.NewGuid(),
			PlayerId: playerId,
			Name: "Incoming",
			Health: new UnevaluatedCharacterHealth.None(),
			Initiative: new UnevaluatedCharacterInitiative("1d20 + 5"),
			ArmourClass: null,
			CharacterOriginDetails: CharacterOriginDetails.CustomCharacter(),
			Hidden: false,
			CopyNumber: null);

		var existingChar1 = new InitiativeCharacter(
			Id: Guid.NewGuid(),
			PlayerId: playerId,
			Name: "Char1",
			Initiative: new CharacterInitiative([new(6, "", ""), new(2, "", "")]),
			Health: new CharacterHealth.None(),
			ArmourClass: null,
			Hidden: false,
			CharacterOriginDetails: CharacterOriginDetails.CustomCharacter(),
			CopyNumber: null,
			Conditions: []
		);

		var existingChar2 = new InitiativeCharacter(
			Id: Guid.NewGuid(),
			PlayerId: playerId,
			Name: "Char2",
			Initiative: new CharacterInitiative([new(6, "", ""), new(5, "", "")]),
			Health: new CharacterHealth.None(),
			ArmourClass: null,
			Hidden: false,
			CharacterOriginDetails: CharacterOriginDetails.CustomCharacter(),
			CopyNumber: null,
			Conditions: []
		);

		diceRoller.EvaluateRoll("1d20 + 5").Returns(new DiceRoll(6, "", ""));
		int callCount = 1;
		diceRoller.RollD20().Returns((CallInfo callInfo) =>
		{
			return new DiceRoll(callCount, "1d20", $"1d20({callCount}) = {callCount++}");
		});
		var result = initiativeRoller.ComputeRolls(
			[incomingChar],
			[existingChar1, existingChar2]
		);
		result.Should().Succeed();

		var existingChar1AfterRoll = result.Value[existingChar1.Id];
		existingChar1AfterRoll.Should().NotBeNull();
		existingChar1AfterRoll!.Value.Select(x => x.Total).SequenceEqual([6, 2]).Should().BeTrue();

		var existingChar2AfterRoll = result.Value[existingChar2.Id];
		existingChar2AfterRoll.Should().NotBeNull();
		existingChar2AfterRoll!.Value.Select(x => x.Total).SequenceEqual([6, 5]).Should().BeTrue();

		var newCharAfterRoll = result.Value[incomingChar.Id];
		newCharAfterRoll.Should().NotBeNull();
		newCharAfterRoll!.Value.Select(x => x.Total).SequenceEqual([6, 1]).Should().BeTrue();
	}

	[Fact]
	public void MergesRollsSuccessfully_CollideOnMerge()
	{
		Guid playerId = Guid.NewGuid();
		var incomingChar = new StagedCharacter(
			Id: Guid.NewGuid(),
			PlayerId: playerId,
			Name: "Incoming",
			Initiative: new UnevaluatedCharacterInitiative("1d20 + 3"),
			Health: new UnevaluatedCharacterHealth.None(),
			ArmourClass: null,
			Hidden: false,
			CharacterOriginDetails: CharacterOriginDetails.CustomCharacter(),
			CopyNumber: null
		);
		diceRoller.EvaluateRoll("1d20 + 3").Returns(new DiceRoll(6, "", ""));

		var existingChar1 = new InitiativeCharacter(
			Id: Guid.NewGuid(),
			PlayerId: playerId,
			Name: "Char1",
			Initiative: new CharacterInitiative([new(6, "", ""), new(2, "", "")]),
			Health: new CharacterHealth.None(),
			ArmourClass: null,
			Hidden: false,
			CharacterOriginDetails: CharacterOriginDetails.CustomCharacter(),
			CopyNumber: null,
			Conditions: []
		);

		var existingChar2 = new InitiativeCharacter(
			Id: Guid.NewGuid(),
			PlayerId: playerId,
			Name: "Char2",
			Initiative: new CharacterInitiative([new(6, "", ""), new(5, "", "")]),
			Health: new CharacterHealth.None(),
			ArmourClass: null,
			Hidden: false,
			CharacterOriginDetails: CharacterOriginDetails.CustomCharacter(),
			CopyNumber: null,
			Conditions: []
		);


		int callCount = 5;
		diceRoller.RollD20().Returns((CallInfo callInfo) =>
		{
			return new DiceRoll(callCount, "1d20", $"1d20({callCount}) = {callCount++}");
		});
		var result = initiativeRoller.ComputeRolls(
			[incomingChar],
			[existingChar1, existingChar2]
		);
		result.Should().Succeed();

		var existingChar1AfterRoll = result.Value[existingChar1.Id];
		existingChar1AfterRoll.Should().NotBeNull();
		existingChar1AfterRoll!.Value.Select(x => x.Total).SequenceEqual([6, 2]).Should().BeTrue();

		var existingChar2AfterRoll = result.Value[existingChar2.Id];
		existingChar2AfterRoll.Should().NotBeNull();
		existingChar2AfterRoll!.Value.Select(x => x.Total).SequenceEqual([6, 5, 6]).Should().BeTrue();

		var newCharAfterRoll = result.Value[incomingChar.Id];
		newCharAfterRoll.Should().NotBeNull();
		newCharAfterRoll!.Value.Select(x => x.Total).SequenceEqual([6, 5, 7]).Should().BeTrue();
	}

	[Fact]
	public void GroupRollsByPrefixTests()
	{
		var entityId1 = Guid.NewGuid();
		var entityId2 = Guid.NewGuid();
		var entityId3 = Guid.NewGuid();

		InitiativeRoller.PrefixGrouping[] groupedRolls = InitiativeRoller.GroupRollsByPrefix(1,
			new Dictionary<Guid, CharacterInitiative>()
			{
				{ entityId1, new CharacterInitiative([new(6, "", "")]) },
				{ entityId2, new CharacterInitiative([new(6, "", ""), new(5, "", "")]) },
				{ entityId3, new CharacterInitiative([new(6, "", ""), new(2, "", "")]) },
			});

		groupedRolls.Should().BeEquivalentTo(new InitiativeRoller.PrefixGrouping[]
		{
			new([new DiceRoll(6, "", "")], [
				new(entityId1, [new(6, "", "")]),
				new(entityId2, [new(6, "", ""), new(5, "", "")]),
				new(entityId3, [new(6, "", ""), new(2, "", "")])
			])
		});
	}

	[Fact]
	public void GroupByWithInitiativeComparerTests()
	{
		DiceRoll[][] input =
		[
			[new(6, "", "")],
			[new(6, "", "")]
		];
		var result = input.GroupBy(x => x, new InitiativeComparer()).ToArray();
		result.Length.Should().Be(1);
	}
}