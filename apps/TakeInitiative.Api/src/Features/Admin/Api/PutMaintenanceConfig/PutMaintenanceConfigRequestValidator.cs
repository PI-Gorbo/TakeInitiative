using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Features.Admin;

public class PutMaintenanceConfigRequestValidator : Validator<PutMaintenanceConfigRequest>
{
    public PutMaintenanceConfigRequestValidator()
    {
        When(x => x.InMaintenanceMode, () =>
        {
            RuleFor(x => x.Reason).NotNull();
        }).Otherwise(() =>
        {
            RuleFor(x => x.Reason).Null();
        });
    }
}
