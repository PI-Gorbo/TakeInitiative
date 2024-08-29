using FluentValidation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features;

public class UnevaluatedCharacterHealthValidator : AbstractValidator<UnevaluatedCharacterHealth>
{
    public UnevaluatedCharacterHealthValidator(IDiceRoller roller)
    {
        RuleFor(x => x)
            .NotNull()
            .SetInheritanceValidator(cfg =>
            {
                cfg.Add(new UnevaluatedCharacterHealthRollValidator(roller));
            });

    }
}
