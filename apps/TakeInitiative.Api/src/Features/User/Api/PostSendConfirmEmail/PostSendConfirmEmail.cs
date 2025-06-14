using System.Net;
using FastEndpoints;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Users;

public class PostSendConfirmEmail(
    IDocumentSession session,
    ConfirmEmailSender confirmEmailSender
    ) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/api/sendConfirmEmail");
        Tags("AllowAnonymous");
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var user = await session.LoadAsync<ApplicationUser>(userId);
        if (user == null)
        {
            ThrowError("User not found", (int)HttpStatusCode.NotFound);
        }

        var confirmResult = await confirmEmailSender.SendConfirmAccountEmail(user, ct);
        if (confirmResult.IsFailure)
        {
            ThrowError($"Failed to send confirmation email. {confirmResult.Error}", (int)HttpStatusCode.BadRequest);
        }

        await SendOkAsync(ct);
    }
}