using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;

public class StagedCombatCharacterDtoValidator : CharacterValidator<StagedCombatCharacterDto>
{
    public StagedCombatCharacterDtoValidator(IDiceRoller roller) : base(roller)
    {

    }
}

