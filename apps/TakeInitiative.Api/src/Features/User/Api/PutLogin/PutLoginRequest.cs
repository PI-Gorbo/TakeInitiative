namespace TakeInitiative.Api.Features.Users;

public record PutLoginRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}
