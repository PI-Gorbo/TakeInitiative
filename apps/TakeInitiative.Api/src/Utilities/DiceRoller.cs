using System.Collections.Immutable;
using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using Marten;
using Marten.Exceptions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Python.Runtime;
using TakeInitiative.Api.Features;

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

    private static Result<List<CharacterInitiativeRoll>> ComputeFirstRollsOfCombat_Recursive(IEnumerable<CombatCharacter> characters, bool isFirstRoll)
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
                var recursivelyComputedRolls = ComputeFirstRollsOfCombat_Recursive(charactersOfGroup, false).GetValueOrDefault(new());
                return group
                    .Select(groupedValue =>
                        new CharacterInitiativeRoll(id: groupedValue.id, rolls: recursivelyComputedRolls.First(x => groupedValue.id == x.id).rolls.Prepend(groupedValue.roll).ToArray())
                    )
                    .ToList()!;
            }).SelectMany(x => x)
            .ToList();
    }

    public static Result<List<CharacterInitiativeRoll>> ComputeFirstRollsOfCombat(IEnumerable<CombatCharacter> characters)
    {
        return ComputeFirstRollsOfCombat_Recursive(characters, isFirstRoll: true);
    }


    private static CharacterInitiativeRoll MergeIncomingRollWithExistingInitiativeList(CharacterInitiativeRoll incomingRoll, IEnumerable<CharacterInitiativeRoll> currentInitiative)
    {
        int initiativeRound = 0;
        var conflicts = currentInitiative.Where(x => x.rolls.Length > initiativeRound && x.rolls[initiativeRound] == incomingRoll.rolls[initiativeRound]);
        while (conflicts.Any())
        {
            // There are one or more conflicts.
            initiativeRound++;
            incomingRoll.rolls.Append(DiceRoller.EvaluateRoll("1d20").Value);
            conflicts = conflicts.Where(x => x.rolls.Length > initiativeRound && x.rolls[initiativeRound] == incomingRoll.rolls[initiativeRound]);
        }

        return incomingRoll;
    }

    public static Result<List<CharacterInitiativeRoll>> ComputeRollsWithExistingInitiative(IEnumerable<CombatCharacter> characters, ImmutableList<CombatCharacter> currentInitiativeList)
    {
        // 1. Compute the rolls of the new characters, to produce a set that has no conflicts.
        var incomingComputedRolls = ComputeFirstRollsOfCombat(characters);
        if (incomingComputedRolls.IsFailure)
        {
            return incomingComputedRolls;
        }

        // 2. Merge the newly computed set into the existing initiative list, one by one.
        List<CharacterInitiativeRoll> outgoingList = currentInitiativeList.Select(x => new CharacterInitiativeRoll(x.Id, x.InitiativeValue)).ToList();
        foreach (CharacterInitiativeRoll incomingRoll in incomingComputedRolls.Value)
        {
            CharacterInitiativeRoll evaluatedRoll = MergeIncomingRollWithExistingInitiativeList(incomingRoll, outgoingList);
            outgoingList = outgoingList.Append(evaluatedRoll).ToList();
        }

        // Return only the new rolls, by filtering by the current initiative list.
        Guid[] existingIds = currentInitiativeList.Select(x => x.Id).ToArray();
        return outgoingList.Where(x => !x.id.In(existingIds)).ToList();
    }
}