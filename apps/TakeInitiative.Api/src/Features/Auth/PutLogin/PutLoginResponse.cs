namespace TakeInitiative.Api.Features.Auth;

public record PutLoginResponse
{
    public required string Token { get; set; }
}
