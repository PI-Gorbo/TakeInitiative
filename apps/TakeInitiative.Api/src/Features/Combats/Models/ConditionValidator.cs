using FluentValidation;

namespace TakeInitiative.Api.Features.Combats;
public class ConditionValidator : AbstractValidator<Condition>
{
    public ConditionValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty();
    }
}