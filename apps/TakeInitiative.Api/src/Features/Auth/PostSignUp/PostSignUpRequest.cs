using FastEndpoints;
using FluentValidation;

namespace TakeInitiative.Api.Features.Auth;

public record PostSignUpRequest
{
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
}

public class PostSignUpRequestValidator : Validator<PostSignUpRequest>
{
    public PostSignUpRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty();

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .Must(password => password.Length >= 6).WithMessage("Passwords must be at least 6 characters long")
            .Must(password => password.Any(character => Char.IsUpper(character))).WithMessage("Passwords must have at least one uppercase character")
            .Must(password => password.Any(character => Char.IsLower(character))).WithMessage("Passwords must have at least one lowercase character")
            .Must(password => password.Any(character => Char.IsDigit(character))).WithMessage("Passwords must have at least one digit")
            .Must(password => password.Any(character => Char.IsPunctuation(character))).WithMessage("Passwords must contain at least one non-alphanumeric character");

    }
}
