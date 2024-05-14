using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Features.Combats;

public class DeleteInitiativeCharacterRequestValidator : Validator<DeleteInitiativeCharacterRequest>
{
    public DeleteInitiativeCharacterRequestValidator()
    {
        RuleFor(x => x.CombatId)
            .NotEmpty();

        RuleFor(x => x.CharacterId)
            .NotEmpty();
    }
}

