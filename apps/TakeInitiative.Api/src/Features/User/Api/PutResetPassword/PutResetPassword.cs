using System.Text;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Users;

public class PutResetPassword(
    UserManager<ApplicationUser> UserManager
    ) : Endpoint<PutResetPasswordRequest>
{
    public override void Configure()
    {
        Put("/api/resetPassword");
        Tags("AllowAnonymous");
    }
    public override async Task HandleAsync(PutResetPasswordRequest req, CancellationToken ct)
    {
        var result = await Result.Try(async () => await UserManager.FindByEmailAsync(Uri.UnescapeDataString(req.Email)), ApiError.DbInteractionFailed)
            .EnsureNotNull("User not found")
            .Bind(async (user) =>
            {
                var result = await UserManager.ResetPasswordAsync(user, Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(req.Token)), req.Password);
                if (result.Succeeded)
                {
                    return UnitResult.Success<ApiError>();
                }

                return UnitResult.Failure<ApiError>(
                    string.Join(", ", result.Errors.Select(x => x.Description))
                );
            });

        await this.ReturnApiResult(result);
    }
}