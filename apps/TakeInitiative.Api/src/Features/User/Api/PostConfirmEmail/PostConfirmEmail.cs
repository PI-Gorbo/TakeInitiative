using System.Net;
using System.Text;
using CSharpFunctionalExtensions;
using FastEndpoints;
using FastEndpoints.Security;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Users;

public class PostConfirmEmail(
    IDocumentSession session,
    UserManager<ApplicationUser> userManager
    ) : Endpoint<PostConfirmEmailRequest, GetUserResponse>
{
    public override void Configure()
    {
        Post("/api/confirmEmail");
        Tags("AllowAnonymous");
    }
    public override async Task HandleAsync(PostConfirmEmailRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var user = await session.LoadAsync<ApplicationUser>(userId);
        if (user == null)
        {
            ThrowError("User not found", (int)HttpStatusCode.NotFound);
        }

        if (!user.EmailConfirmed)
        {
            var confirmResult = await userManager.ConfirmEmailAsync(user, Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(req.ConfirmEmailToken!)));
            if (!confirmResult.Succeeded)
            {
                ThrowError(string.Join(", ", confirmResult.Errors), (int)HttpStatusCode.BadRequest);
            }
        }

        var getUserResponse = GetUserResponse.Generate(session, userId);
        await this.ReturnApiResult(getUserResponse);
    }
}