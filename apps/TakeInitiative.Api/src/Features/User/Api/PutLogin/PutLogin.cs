using CSharpFunctionalExtensions;
using FastEndpoints;
using FastEndpoints.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Users;
public class PutLogin(
    IOptions<JWTOptions> JWTOptions,
    UserManager<ApplicationUser> UserManager,
    SignInManager<ApplicationUser> SignInManager) : Endpoint<PutLoginRequest>
{
    public override void Configure()
    {
        Put("/api/login");
        AllowAnonymous();
    }
    public override async Task HandleAsync(PutLoginRequest req, CancellationToken ct)
    {
        var result = await Result
            .Try(
                async () => await UserManager.FindByEmailAsync(req.Email),
                (err) => ApiError.InternalServerError(err.Message))
            .EnsureNotNull(ApiError.Unauthorized("Invalid username or password"))
            .Bind(async (user) =>
            {
                // Validate password
                var signInResult = await SignInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: false);
                if (!signInResult.Succeeded)
                {
                    return ApiError.Unauthorized("Invalid username or password");
                }

                // Create cookie
                await CookieAuth.SignInAsync(u =>
                {
                    u["UserId"] = user.Id.ToString();
                });

                return UnitResult.Success<ApiError>();
            });

        await this.ReturnApiResult(result);
    }
}