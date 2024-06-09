namespace TakeInitiative.Api.Features.Users;

public record PostConfirmEmailRequest
{
    public string ConfirmEmailToken {get; set;}
}
