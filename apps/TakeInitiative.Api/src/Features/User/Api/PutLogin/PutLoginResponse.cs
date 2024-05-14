namespace TakeInitiative.Api.Features.Users;

public record PutLoginResponse
{
    public required string Token { get; set; }
}
