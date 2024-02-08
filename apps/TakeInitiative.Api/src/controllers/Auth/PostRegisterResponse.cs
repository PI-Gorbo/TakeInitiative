namespace TakeInitiative.Api.Controllers;

public record PostRegisterResponse
{
    public required string Token { get; set; }
}
