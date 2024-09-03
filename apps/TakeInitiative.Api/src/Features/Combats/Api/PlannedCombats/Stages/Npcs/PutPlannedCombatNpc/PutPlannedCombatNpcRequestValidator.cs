using FastEndpoints;
using FluentValidation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;

public class PutPlannedCombatNpcRequestValidator : Validator<PutPlannedCombatNpcRequest>
{
    public PutPlannedCombatNpcRequestValidator(IDiceRoller roller)
    {
        RuleFor(x => x.CombatId)
            .NotEmpty();

        RuleFor(x => x.StageId)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty();

        RuleFor(x => x.Initiative)
            .NotEmpty()
            .SetValidator(new UnevaluatedCharacterInitiativeValidator(roller));

        RuleFor(x => x.Quantity)
            .Must(quantity => quantity >= 1)
            .WithMessage("Quantity must be equal to or greater than 1.");
    }
}
