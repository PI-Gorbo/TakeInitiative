using FastEndpoints;
using FluentValidation;
using TakeInitiative.Api.Features;

namespace TakeInitiative.Api.Features.Combats;

public class PutPlannedCombatNpcRequestValidator : Validator<PutPlannedCombatNpcRequest>
{
    public PutPlannedCombatNpcRequestValidator()
    {
        RuleFor(x => x.CombatId)
            .NotEmpty();

        RuleFor(x => x.StageId)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty();

        RuleFor(x => x.Health.CurrentHealth)
            .NotEmpty()
            .When(x => x.Health != null);

        RuleFor(x => x.Initiative)
            .NotEmpty()
            .SetValidator(new CharacterInitiativeValidator());

        RuleFor(x => x.Quantity)
            .Must(quantity => quantity >= 1)
            .WithMessage("Quantity must be equal to or greater than 1.");
    }
}
