using System.Collections.Immutable;
using CSharpFunctionalExtensions;

namespace TakeInitiative.Utilities;

public interface IInitiativeRoller
{
    public Result<List<CharacterInitiativeRoll>> ComputeRolls(IEnumerable<CombatCharacter> characters);

    public Result<List<CharacterInitiativeRoll>> ComputeRolls(List<CombatCharacter> newCharacters, List<CombatCharacter> existingInitiativeList);
}

