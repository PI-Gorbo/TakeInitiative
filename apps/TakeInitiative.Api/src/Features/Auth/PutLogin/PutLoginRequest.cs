namespace TakeInitiative.Api.Features.Auth;

public record PutLoginRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}
