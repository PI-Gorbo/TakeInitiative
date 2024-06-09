using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Features.Users;

public class PutSendResetPasswordEmailRequestValidator : Validator<PutSendResetPasswordEmailRequest>
{
    public PutSendResetPasswordEmailRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();
    }
}
