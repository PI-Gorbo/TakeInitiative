using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Marten;
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

    public static Result<List<CharacterInitiativeRoll>> ComputeFirstRolls(IEnumerable<CombatCharacter> characters)
    {
        return ComputeFirstRollsOfCombat_Recursive(characters, isFirstRoll: true);
    }


    // private static CharacterInitiativeRoll MergeIncomingRollWithExistingInitiativeList(CharacterInitiativeRoll incomingRoll, List<CharacterInitiativeRoll> currentInitiative)
    // {
    //     // Situation:
    //     // We have an incoming roll, lets say 6, which has to be merged in with the other rolls.

    //     int initiativeRound = 0;
    //     var conflicts = currentInitiative.Where(x => x.rolls.Length > initiativeRound && x.rolls[initiativeRound] == incomingRoll.rolls[initiativeRound]).ToList();
    //     while (conflicts.Any())
    //     {
    //         // If the incoming roll needs to be appended to (as we are at the end of the roll and there are conflicts)
    //         if (incomingRoll.rolls.Length == initiativeRound)
    //         {
    //             incomingRoll.rolls.Append(DiceRoller.EvaluateRoll("1d20").Value);
    //         }

    //         // Search for more conflicts, until there are none.
    //         initiativeRound++;
    //         if (conflicts.Count == 1)
    //         {
    //             // If there is only one conflict, we need to extend the initiative list of the current initiative rolls too.
    //             Guid conflictId = conflicts[0].id;

    //         }
    //         else
    //         {
    //             conflicts = currentInitiative.Where(x => x.rolls.Length > initiativeRound && x.rolls[initiativeRound] == incomingRoll.rolls[initiativeRound]).ToList();
    //         }
    //     }

    //     return incomingRoll;
    // }

    public static Result<List<CharacterInitiativeRoll>> ComputeRolls(List<CombatCharacter> newCharacters, ImmutableList<CombatCharacter> existingInitiativeList)
    {
        // 1. Compute the rolls of the new characters, to produce a set that has no conflicts.
        var incomingComputedRolls = ComputeFirstRolls(newCharacters);
        if (incomingComputedRolls.IsFailure)
        {
            return incomingComputedRolls;
        }

        Dictionary<Guid, CharacterInitiativeRoll> outgoingCharacterInitiative = existingInitiativeList
            .Select(x => new CharacterInitiativeRoll(x.Id, x.InitiativeValue))
            .Concat(incomingComputedRolls.Value)
            .ToDictionary(x => x.id, x => x);

        int initiativeRound = 0;
        List<IGrouping<int, CharacterInitiativeRoll>> rollsGroupedByConflict = outgoingCharacterInitiative
            .Values
            .Where(x => x.rolls.Length > initiativeRound)
            .GroupBy(x => x.rolls[initiativeRound])
            .ToList();

        while (rollsGroupedByConflict.Select(x => x.Count() > 1).Any())
        {

            foreach (var group in rollsGroupedByConflict)
            {

                if (group.Count() == 1)
                {
                    continue; // No Conflict, skip.
                }

                // The list of all the ids of the elements that need to be extended.
                Guid[] idsThatNeedToBeExtended = Array.Empty<Guid>();

                if (group.All(x => x.rolls.Length == initiativeRound + 1))
                {
                    // Situation:
                    // Incoming: (id1,6)
                    // Current: (id2,6)
                    // Here, we need to extend both
                    idsThatNeedToBeExtended = group.Select(x => x.id).ToArray();
                }
                // else if ()
                // {
                //     // Situation:
                //     // Incoming: (id1, [6,2])
                //     // Current: (id2, [6,3])
                //     // Here, we don't need to extend, we just need to increment the initiative round by 1.
                // }
                else
                {


                }


                initiativeRound++;
            }

            // Re-compute if there are any conflicts.
            rollsGroupedByConflict = outgoingCharacterInitiative
                .Values
                .Where(x => x.rolls.Length > initiativeRound)
                .GroupBy(x => x.rolls[initiativeRound])
                .ToList();
        }

        return rollsGroupedByConflict.SelectMany(x => x).ToList();
    }
}