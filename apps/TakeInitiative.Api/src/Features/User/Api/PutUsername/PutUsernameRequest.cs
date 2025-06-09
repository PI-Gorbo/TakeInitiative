namespace TakeInitiative.Api.Features.Users;

public record PutUsernameRequest
{
    public required string NewUsername { get; set; }
}
