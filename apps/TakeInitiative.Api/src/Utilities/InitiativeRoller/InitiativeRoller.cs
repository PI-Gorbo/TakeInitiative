using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Marten;

namespace TakeInitiative.Utilities;

public class InitiativeRoller(IDiceRoller roller) : IInitiativeRoller
{
    public Result<List<CharacterInitiativeRoll>> ComputeRolls(IEnumerable<StagedCharacter> characters)
    {
        return ComputeRolls_Recursive(characters, isFirstRoll: true);
    }

    public Result<List<CharacterInitiativeRoll>> ComputeRolls(List<StagedCharacter> newCharacters, List<InitiativeCharacter> existingInitiativeList)
    {
        // 1. Compute the rolls of the new characters, to produce a set that has no conflicts.
        var incomingComputedRolls = ComputeRolls(newCharacters);
        if (incomingComputedRolls.IsFailure)
        {
            return incomingComputedRolls;
        }

        return MergeRolls(existingInitiativeList, incomingComputedRolls);
    }

    internal Result<List<CharacterInitiativeRoll>> ComputeRolls_Recursive(IEnumerable<StagedCharacter> characters, bool isFirstRoll)
    {
        // 1. For the input list, compute the rolls.
        var computedRollsResult = ComputeOneRollForEachCharacter(characters, isFirstRoll);
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

    internal Result<List<CharacterInitiativeRoll>> MergeRolls(List<InitiativeCharacter> existingInitiativeList, Result<List<CharacterInitiativeRoll>> incomingComputedRolls)
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
    internal Result<List<(Guid id, int roll)>> ComputeOneRollForEachCharacter(IEnumerable<StagedCharacter> characters, bool isFirstRoll)
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
}

