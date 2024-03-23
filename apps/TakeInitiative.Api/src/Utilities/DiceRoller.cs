using System.Collections.Immutable;
using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using Marten;
using Marten.Exceptions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Python.Runtime;
using TakeInitiative.Api.Models;

namespace TakeInitiative.Utilities;

public static class DiceRoller
{

	public static Result<int> EvaluateRoll(string roll)
	{
		return Result.Try(() =>
		{
			using (Py.GIL())
			{
				dynamic d20 = Py.Import("d20");
				var result = d20.roll(roll);
				return (int)result.total;
			};
		}, ex => $"Failed to evaluate dice roll, please check your syntax. {ex}");
	}

	private static Result<List<(Guid id, int roll)>> ComputeRollsForGroup(IEnumerable<CombatCharacter> characters, bool isFirstRoll)
	{
		return characters.Select(x => (id: x.Id, roll: isFirstRoll ? x.Initiative.RollInitiative() : DiceRoller.EvaluateRoll("1d20")))
			.Aggregate(Result.Success<List<(Guid id, int roll)>>(new()), (current, nextValue) =>
			{
				if (current.IsFailure)
				{
					return current.MapError(x => x + (nextValue.roll.IsFailure ? $", {nextValue.roll.Error}" : ""));
				}


				if (nextValue.roll.IsFailure)
				{
					return nextValue.roll.ConvertFailure<List<(Guid id, int roll)>>();
				}

				return current.Value.Append((id: nextValue.id, roll: nextValue.roll.Value)).ToList();
			});
	}

	private static Result<List<CharacterInitiativeRoll>> ComputeRolls_Recursive(IEnumerable<CombatCharacter> characters, bool isFirstRoll)
	{
		// 1. For the input list, compute the rolls.
		var computedRollsResult = ComputeRollsForGroup(characters, isFirstRoll);
		if (computedRollsResult.IsFailure)
		{
			return computedRollsResult.ConvertFailure<List<CharacterInitiativeRoll>>();
		}

		// 2. Determine any groupings of those rolls.
		return computedRollsResult.Value
			.GroupBy(x => x.roll)
			.Select(group =>
			{
				if (group.Count() == 1)
				{
					return new List<CharacterInitiativeRoll>() { new(group.First().id, new[] { group.Key }) };
				}

				var ids = group.Select(x => x.id).ToArray();
				var charactersOfGroup = characters.Where(x => x.Id.IsOneOf(ids));
				// Re-Roll for characters of the group.
				var recursivelyComputedRolls = ComputeRolls_Recursive(charactersOfGroup, false).GetValueOrDefault(new());
				return group
					.Select(groupedValue =>
						new CharacterInitiativeRoll(id: groupedValue.id, rolls: recursivelyComputedRolls.First(x => groupedValue.id == x.id).rolls.Prepend(groupedValue.roll).ToArray())
					)
					.ToList()!;
			}).SelectMany(x => x)
			.ToList();
	}


	public static Result<List<CharacterInitiativeRoll>> ComputeRolls(IEnumerable<CombatCharacter> characters)
	{
		return ComputeRolls_Recursive(characters, isFirstRoll: true);
	}
}