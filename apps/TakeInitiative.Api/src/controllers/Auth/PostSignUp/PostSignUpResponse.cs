namespace TakeInitiative.Api.Controllers;

public record PostSignUpResponse
{
    public required string Token { get; set; }
}
