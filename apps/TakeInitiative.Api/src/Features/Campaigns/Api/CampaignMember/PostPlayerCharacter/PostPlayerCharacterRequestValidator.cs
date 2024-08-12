using FastEndpoints;
using FluentValidation;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Campaigns;

public class PostPlayerCharacterRequestValidator : Validator<PostPlayerCharacterRequest>
{
    public PostPlayerCharacterRequestValidator(IDiceRoller roller)
    {
        RuleFor(x => x.CampaignMemberId)
            .NotEmpty();

        RuleFor(x => x.PlayerCharacter)
            .SetValidator(new PlayerCharacterDTOValidator(roller));
    }
}
