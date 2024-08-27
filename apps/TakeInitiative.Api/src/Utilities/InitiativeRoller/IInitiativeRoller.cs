using System.Collections.Immutable;
using CSharpFunctionalExtensions;

namespace TakeInitiative.Utilities;

public interface IInitiativeRoller
{
    public Result<List<CharacterInitiativeRoll>> ComputeRolls(IEnumerable<StagedCharacter> characters);

    public Result<List<CharacterInitiativeRoll>> ComputeRolls(List<StagedCharacter> newCharacters, List<InitiativeCharacter> existingInitiativeList);
}

