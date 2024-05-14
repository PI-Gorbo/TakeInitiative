using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Features.Combats;

public class PutUpdateInitiativeCharacterRequestValidator : Validator<PutUpdateInitiativeCharacterRequest>
{
    public PutUpdateInitiativeCharacterRequestValidator()
    {
        RuleFor(x => x.CombatId)
            .NotEmpty();

        RuleFor(x => x.Character)
            .NotEmpty();
    }
}

