using System.Net;
using FastEndpoints;
using FastEndpoints.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace TakeInitiative.Api.Features.Users;

public class PutSendResetPasswordEmail(
    UserManager<ApplicationUser> UserManager,
    ResetPasswordEmailSender resetPasswordEmailSender
    ) : Endpoint<PutSendResetPasswordEmailRequest>
{
    public override void Configure()
    {
        Put("/api/sendResetPasswordEmail");
        AllowAnonymous();
    }
    public override async Task HandleAsync(PutSendResetPasswordEmailRequest req, CancellationToken ct)
    {
        var user = await UserManager.FindByEmailAsync(req.Email);
        if (user == null)
        {
            // There is no user with this username / password. We do not want to reveal this information un-nessisarily.
            await SendOkAsync(ct); // Send Ok 200. 
            return;
        }

        // Send Email Authentication.
        await resetPasswordEmailSender.SendResetPasswordEmail(user, ct);
        await SendOkAsync(ct);
    }
}