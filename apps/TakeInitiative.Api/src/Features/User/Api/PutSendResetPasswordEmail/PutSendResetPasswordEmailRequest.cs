namespace TakeInitiative.Api.Features.Users;

public record PutSendResetPasswordEmailRequest
{
    public required string Email { get; set; }
}
