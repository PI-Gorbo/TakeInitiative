namespace TakeInitiative.Api.Controllers;

public record PostSignUpRequest
{
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
}
