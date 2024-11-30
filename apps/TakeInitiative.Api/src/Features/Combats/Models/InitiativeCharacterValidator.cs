using FluentValidation;

namespace TakeInitiative.Api.Features.Combats;

public class InitiativeCharacterValidator : AbstractValidator<InitiativeCharacter>
{
    public InitiativeCharacterValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty();

        RuleFor(x => x.Health)
            .NotNull();

        RuleFor(x => x.PlayerId)
            .NotEmpty();
    }
}

