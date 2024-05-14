namespace TakeInitiative.Api.Features.Auth;

public record PostSignUpResponse
{
    public required string Token { get; set; }
}
