using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Features.Users;

public class PostConfirmEmailRequestValidator : Validator<PostConfirmEmailRequest>
{
    public PostConfirmEmailRequestValidator()
    {
        RuleFor(x => x.ConfirmEmailToken)
          .NotEmpty();
    }
}
