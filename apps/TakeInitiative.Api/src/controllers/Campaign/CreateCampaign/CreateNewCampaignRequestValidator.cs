using FluentValidation;
namespace TakeInitiative.Data.Commands;

public class CreateNewCampaignRequestValidator : AbstractValidator<CreateCampaignRequest>
{
    public CreateNewCampaignRequestValidator()
    {
        RuleFor(x => x.CampaignName)
            .NotEmpty();
    }
}
