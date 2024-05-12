using System.Net;
using FastEndpoints;
using FastEndpoints.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using TakeInitiative.Api.Bootstrap;
using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Features;
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
        var user = await UserManager.FindByEmailAsync(req.Email);
        if (user == null)
        {
            ThrowError("Invalid username or password", (int)HttpStatusCode.Unauthorized);
        }

        // Validate password
        var signInResult = await SignInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: false);
        if (!signInResult.Succeeded)
        {
            ThrowError("Invalid username or password", (int)HttpStatusCode.Unauthorized);
        }

        await CookieAuth.SignInAsync(u =>
        {
            u["UserId"] = user.Id.ToString();
        });

        await SendOkAsync(ct);
    }
}