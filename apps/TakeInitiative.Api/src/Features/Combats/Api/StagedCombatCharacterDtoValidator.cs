using FluentValidation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;

public class StagedCombatCharacterDtoValidator : CharacterValidator<StagedCombatCharacterDto>
{
    public StagedCombatCharacterDtoValidator(IDiceRoller roller) : base(roller)
    {
        RuleFor(x => x.Conditions)
            .NotNull();

        RuleForEach(x => x.Conditions)
            .SetValidator(new ConditionValidator());
    }
}

