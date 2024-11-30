using CSharpFunctionalExtensions;
using TakeInitiative.Api.Features;

namespace TakeInitiative.Utilities;

public interface IInitiativeRoller
{
    public Result<Dictionary<Guid, CharacterInitiative>> ComputeRolls(IEnumerable<StagedCharacter> characters);

    public Result<Dictionary<Guid, CharacterInitiative>> ComputeRolls(List<StagedCharacter> newCharacters, List<InitiativeCharacter> existingInitiativeList);
}

