namespace TakeInitiative.Api.Features.Users;

public record PostSignUpResponse
{
    public required string Token { get; set; }
}
