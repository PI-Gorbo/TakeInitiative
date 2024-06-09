namespace TakeInitiative.Api.Features.Users;

public record PutResetPasswordRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
    public required string Token { get; set; }
}
