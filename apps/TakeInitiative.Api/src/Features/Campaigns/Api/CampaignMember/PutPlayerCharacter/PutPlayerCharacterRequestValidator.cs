using FastEndpoints;
using FluentValidation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Campaigns;

public class PutPlayerCharacterRequestValidator : Validator<PutPlayerCharacterRequest>
{
    public PutPlayerCharacterRequestValidator(IDiceRoller roller)
    {
        RuleFor(x => x.CampaignMemberId).NotEmpty();
        RuleFor(x => x.PlayerCharacterId).NotEmpty();
        RuleFor(x => x.PlayerCharacter)
            .SetValidator(new PlayerCharacterDTOValidator(roller));
    }
}
