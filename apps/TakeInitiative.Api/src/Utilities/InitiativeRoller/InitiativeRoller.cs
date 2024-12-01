using CSharpFunctionalExtensions;

using Marten;

using TakeInitiative.Api.Features;

namespace TakeInitiative.Utilities;

public class InitiativeRoller(IDiceRoller roller) : IInitiativeRoller
{
    public Result<Dictionary<Guid, CharacterInitiative>> ComputeRolls(IEnumerable<StagedCharacter> characters)
    {
        return ComputeRolls_Recursive(characters, isFirstRoll: true);
    }

    public Result<Dictionary<Guid, CharacterInitiative>> ComputeRolls(List<StagedCharacter> newCharacters,
        List<InitiativeCharacter> existingInitiativeList)
    {
        // 1. Compute the rolls of the new characters, to produce a set that has no conflicts.
        var incomingComputedRolls = ComputeRolls(newCharacters);
        if (incomingComputedRolls.IsFailure)
        {
            return incomingComputedRolls;
        }

        return MergeRolls(existingInitiativeList, incomingComputedRolls);
    }

    internal Result<Dictionary<Guid, CharacterInitiative>> ComputeRolls_Recursive(
        IEnumerable<StagedCharacter> characters, bool isFirstRoll)
    {
        // 1. For the input list, compute the rolls.
        var computedRollsResult = ComputeOneRollForEachCharacter(characters, isFirstRoll);
        if (computedRollsResult.IsFailure)
        {
            return computedRollsResult.ConvertFailure<Dictionary<Guid, CharacterInitiative>>();
        }

        // 2. Determine any groupings of those rolls.
        return computedRollsResult.Value
            .GroupBy(x => x.roll.Total)
            .Select(group =>
            {
                if (group.Count() == 1)
                {
                    return new List<Tuple<Guid, DiceRoll[]>>()
                    {
                        new Tuple<Guid, DiceRoll[]>(group.First().id, [group.First().roll])
                    };
                }

                var ids = group.Select(x => x.id).ToArray();
                var charactersOfGroup = characters.Where(x => x.Id.IsOneOf(ids));
                // Re-Roll for characters of the group.
                var recursivelyComputedRolls =
                    ComputeRolls_Recursive(charactersOfGroup, false).GetValueOrDefault(new());
                return group
                    .Select(groupedValue =>
                        new Tuple<Guid, DiceRoll[]>(groupedValue.id,
                            new List<DiceRoll> { groupedValue.roll }
                                .Concat(recursivelyComputedRolls[groupedValue.id].Value).ToArray())
                    ).ToList();
            })
            .SelectMany(x => x)
            .ToDictionary(x => x.Item1, x => new CharacterInitiative(x.Item2));
    }

    internal Result<Dictionary<Guid, CharacterInitiative>> MergeRolls(List<InitiativeCharacter> existingInitiativeList,
        Result<Dictionary<Guid, CharacterInitiative>> incomingComputedRolls)
    {
        // Notes:
        // This method has an interesting role / problem. 
        // Input example 1:
        //  existing: [(Char1, [6])]
        //  incoming: [(Char2, [6, 3]), (Char2, [6, 2])]
        //  - We need to roll for the existing character, to get it up to speed with the incoming, then roll for the entire group!.
        // Input Example 2:
        //  existing: [(Char1, [6, 3])]
        //  incoming: [(Char2, [6, 3])]
        //  - We need to extend these two rolls.
        // Input example 3:
        //  existing: [(Char1, [6, 3, 4])]
        //  incoming: [(Char2, [6, 3])]
        //  - We need to extend the incoming!

        Dictionary<Guid, CharacterInitiative> outgoingCharacterInitiative = existingInitiativeList
            .ToDictionary(x => x.Id, x => x.Initiative)
            .Concat(incomingComputedRolls.Value)
            .ToDictionary(x => x.Key, x => x.Value);

        // Consider that initiative is modeled as an array of values.
        // We can group by common prefix.
        int prefixLength = 1;
        PrefixGrouping[] collisions = [];
        do
        {
            collisions = GroupRollsByPrefix(prefixLength++, outgoingCharacterInitiative).ToArray();
            // What are the possible situations for each grouping?
            // Situation 1:
            // Group looks like: [(Char1, 6, 4), (Char2, 6, 2)], grouping index = 0
            // Here, we can just continue.
            if (collisions.SelectMany(x => x.CharactersWithMatchingRollPrefix).Select(x => x.DiceRoll)
                .All(roll => roll.Length > prefixLength))
            {
            }
        } while (collisions.Length > 0);


        //
        // int rollIndex = 0;
        // List<IGrouping<int, KeyValuePair<Guid, CharacterInitiative>>> rollsGroupedByConflict =
        //     outgoingCharacterInitiative
        //         .Where(x => x.Value.Value.Length > rollIndex)
        //         .GroupBy(x => x.Value.Value[rollIndex].Total)
        //         .ToList();
        //
        // // Loop while there are any conflicts.
        // while (rollsGroupedByConflict.Where(x => x.Count() > 1).Any())
        // {
        //     // Resolve all conflicts.
        //     foreach (var conflictGroup in rollsGroupedByConflict.Where(x => x.Count() > 1))
        //     {
        //         // The list of all the ids of the elements that need to be extended.
        //         Guid[] idsThatNeedToBeExtended = Array.Empty<Guid>();
        //         if (conflictGroup.Select(x => x.Value.Value.Length).Distinct().Count() ==
        //             1) // When all the conflicting elements in the list are the same length.
        //         {
        //             // One of two situations:
        //             //// Situation 1, index = 0 -> here, we just need to increment index (ie: do nothing.)
        //             // Incoming: 6 3
        //             // Current:  6 2 
        //             //           6 3
        //             //// Situation 2, index = 0 -> Here, we need to extend both values in the group.
        //             // Incoming: 6
        //             // Current:  6
        //
        //             var firstRoll = conflictGroup.First();
        //             // Situation 2.
        //             if (rollIndex + 1 == firstRoll.Value.Value.Length)
        //             {
        //                 foreach (var characterRoll in conflictGroup)
        //                 {
        //                     var previousRoll = outgoingCharacterInitiative[characterRoll.Key];
        //                     outgoingCharacterInitiative[characterRoll.Key] =
        //                         new CharacterInitiative([.. previousRoll.Value, roller.RollD20()]);
        //                 }
        //             }
        //         }
        //         else // When all the rolls are not the same length.
        //         {
        //             // Otherwise, the other possible situation is this:
        //             // Incoming: 6
        //             // Current:  6 2
        //             //           6 5
        //             // We just need to extend the short roll(s).
        //             var idsToExtend = conflictGroup.Where(x => rollIndex + 1 == x.Value.Value.Length)
        //                 .Select(x => x.Key);
        //             foreach (var id in idsToExtend)
        //             {
        //                 var previousRoll = outgoingCharacterInitiative[id];
        //                 outgoingCharacterInitiative[id] =
        //                     new CharacterInitiative([.. previousRoll.Value, roller.RollD20()]);
        //             }
        //         }
        //
        //         rollIndex++;
        //     }
        //
        //     // Re-compute if there are any conflicts.
        //     rollsGroupedByConflict = outgoingCharacterInitiative
        //         .Where(x => x.Value.Value.Length > rollIndex)
        //         .GroupBy(x => x.Value.Value[rollIndex].Total)
        //         .ToList();
        // }
        //
        // return outgoingCharacterInitiative;
    }

    internal Result<List<(Guid id, DiceRoll roll)>> ComputeOneRollForEachCharacter(
        IEnumerable<StagedCharacter> characters, bool isFirstRoll)
    {
        return characters.Select(x =>
                (id: x.Id, roll: isFirstRoll ? x.Initiative.RollInitiative(roller) : roller.RollD20()))
            .Aggregate(Result.Success<List<(Guid id, DiceRoll roll)>>(new()), (current, nextValue) =>
            {
                if (current.IsFailure)
                {
                    return current.MapError(x => x + (nextValue.roll.IsFailure ? $", {nextValue.roll.Error}" : ""));
                }

                if (nextValue.roll.IsFailure)
                {
                    return nextValue.roll.ConvertFailure<List<(Guid id, DiceRoll roll)>>();
                }

                return current.Value.Append((id: nextValue.id, roll: nextValue.roll.Value)).ToList();
            });
    }


    internal record CharacterDiceRoll(Guid CharacterId, DiceRoll[] DiceRoll);

    internal record PrefixGrouping(DiceRoll[] RollPrefix, CharacterDiceRoll[] CharactersWithMatchingRollPrefix);

    internal IEnumerable<PrefixGrouping> GroupRollsByPrefix(int prefixLength,
        Dictionary<Guid, CharacterInitiative> characters)
    {
        return characters.Where(x => x.Value.Value.Length <= prefixLength)
            .GroupBy(x => x.Value.Value[0..prefixLength], new InitiativeComparer())
            .Where(x => x.Count() > 1) // Ensure we only return collisions.
            .Select(group =>
                new PrefixGrouping(group.Key,
                    group.Select(x => new CharacterDiceRoll(x.Key, x.Value.Value)).ToArray()));
    }
}