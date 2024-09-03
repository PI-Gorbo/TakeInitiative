using CSharpFunctionalExtensions;
using FluentValidation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features;

// public class UnevaluatedCharacterHealthNoneValidator : AbstractValidator<UnevaluatedCharacterHealth.None> { }
// public class UnevaluatedCharacterHealthFixedValidator : AbstractValidator<UnevaluatedCharacterHealth.Fixed> { }
public class UnevaluatedCharacterHealthRollValidator : AbstractValidator<UnevaluatedCharacterHealth.Roll>
{
    public UnevaluatedCharacterHealthRollValidator(IDiceRoller roller)
    {
        RuleFor(x => x.RollString)
            .NotEmpty()
            .Custom((value, context) =>
                roller
                    .EvaluateRoll(value!)
                    .TapError(context.AddFailure));
    }
}
