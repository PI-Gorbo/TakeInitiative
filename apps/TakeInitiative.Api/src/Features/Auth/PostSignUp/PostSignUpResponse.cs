namespace TakeInitiative.Api.Features;

public record PostSignUpResponse
{
    public required string Token { get; set; }
}
