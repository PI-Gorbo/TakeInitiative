using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Marten;
using Python.Runtime;

namespace TakeInitiative.Utilities;

public static class DiceRollerExtensions
{
    public static int RollD20(this IDiceRoller roller) => roller.EvaluateRoll("1d20").Value;

    public static Result<List<CharacterInitiativeRoll>> ComputeRolls(this IDiceRoller roller, IEnumerable<CombatCharacter> characters)
    {
        return roller.ComputeRolls_Recursive(characters, isFirstRoll: true);
    }

    public static Result<List<CharacterInitiativeRoll>> ComputeRolls(this IDiceRoller roller, List<CombatCharacter> newCharacters, ImmutableList<CombatCharacter> existingInitiativeList)
    {
        // 1. Compute the rolls of the new characters, to produce a set that has no conflicts.
        var incomingComputedRolls = roller.ComputeRolls(newCharacters);
        if (incomingComputedRolls.IsFailure)
        {
            return incomingComputedRolls;
        }

        return roller.MergeRolls(existingInitiativeList, incomingComputedRolls);
    }

    internal static Result<List<CharacterInitiativeRoll>> ComputeRolls_Recursive(this IDiceRoller roller, IEnumerable<CombatCharacter> characters, bool isFirstRoll)
    {
        // 1. For the input list, compute the rolls.
        var computedRollsResult = roller.ComputeOneRollForEachCharacter(characters, isFirstRoll);
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
                var recursivelyComputedRolls = roller.ComputeRolls_Recursive(charactersOfGroup, false).GetValueOrDefault(new());
                return group
                    .Select(groupedValue =>
                        new CharacterInitiativeRoll(id: groupedValue.id, rolls: recursivelyComputedRolls.First(x => groupedValue.id == x.id).rolls.Prepend(groupedValue.roll).ToArray())
                    )
                    .ToList()!;
            }).SelectMany(x => x)
            .ToList();
    }

    internal static Result<List<(Guid id, int roll)>> ComputeOneRollForEachCharacter(this IDiceRoller roller, IEnumerable<CombatCharacter> characters, bool isFirstRoll)
    {
        return characters.Select(x => (id: x.Id, roll: isFirstRoll ? x.Initiative.RollInitiative(roller) : roller.RollD20()))
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

    internal static Result<List<CharacterInitiativeRoll>> MergeRolls(this IDiceRoller roller, ImmutableList<CombatCharacter> existingInitiativeList, Result<List<CharacterInitiativeRoll>> incomingComputedRolls)
    {
        Dictionary<Guid, CharacterInitiativeRoll> outgoingCharacterInitiative = existingInitiativeList
            .Select(x => new CharacterInitiativeRoll(x.Id, x.InitiativeValue))
            .Concat(incomingComputedRolls.Value)
            .ToDictionary(x => x.id, x => x);

        int rollIndex = 0;
        List<IGrouping<int, CharacterInitiativeRoll>> rollsGroupedByConflict = outgoingCharacterInitiative
            .Values
            .Where(x => x.rolls.Length > rollIndex)
            .GroupBy(x => x.rolls[rollIndex])
            .ToList();

        while (rollsGroupedByConflict.Where(x => x.Count() > 1).Any())
        {

            foreach (var group in rollsGroupedByConflict)
            {

                if (group.Count() == 1)
                {
                    continue; // No Conflict, skip.
                }

                // The list of all the ids of the elements that need to be extended.
                Guid[] idsThatNeedToBeExtended = Array.Empty<Guid>();

                if (group.Select(x => x.rolls.Length).Distinct().Count() == 1)
                {
                    // One of two situations:
                    //// Situation 1, index = 0 -> here, we just need to increment index (ie: do nothing.)
                    // Incoming: 6 3
                    // Current:  6 2 
                    //           6 3
                    //// Situation 2, index = 0 -> Here, we need to extend both values in the group.
                    // Incoming: 6
                    // Current:  6
                    var firstRoll = group.First();
                    if (rollIndex + 1 == firstRoll.rolls.Length)
                    {
                        // Situation 2.
                        foreach (var characterRoll in group)
                        {
                            var previousRoll = outgoingCharacterInitiative[characterRoll.id];
                            outgoingCharacterInitiative[characterRoll.id] = previousRoll with
                            {
                                rolls = previousRoll.rolls.Append(roller.RollD20()).ToArray()
                            };
                        }
                    }
                }
                else
                {
                    // Otherwise, the other possible situation is this:
                    // Incoming: 6
                    // Current:  6 2
                    //           6 5
                    // We just need to extend the short roll(s).
                    var idsToExtend = group.Where(x => rollIndex + 1 == x.rolls.Length).Select(x => x.id);
                    foreach (var id in idsToExtend)
                    {
                        var previousRoll = outgoingCharacterInitiative[id];
                        outgoingCharacterInitiative[id] = previousRoll with
                        {
                            rolls = previousRoll.rolls.Append(roller.RollD20()).ToArray()
                        };
                    }
                }

                rollIndex++;
            }

            // Re-compute if there are any conflicts.
            rollsGroupedByConflict = outgoingCharacterInitiative
                .Values
                .Where(x => x.rolls.Length > rollIndex)
                .GroupBy(x => x.rolls[rollIndex])
                .ToList();
        }

        return outgoingCharacterInitiative.Values.ToList();
    }
}

