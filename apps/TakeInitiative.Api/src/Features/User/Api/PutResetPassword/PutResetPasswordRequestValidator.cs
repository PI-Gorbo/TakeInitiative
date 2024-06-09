using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Features.Users;

public class PutResetPasswordRequestValidator : Validator<PutResetPasswordRequest>
{
    public PutResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty();

        RuleFor(x => x.Token)
            .NotEmpty();
    }
}
