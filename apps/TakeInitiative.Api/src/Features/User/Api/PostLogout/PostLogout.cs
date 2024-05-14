using System.Net;
using FastEndpoints;
using FastEndpoints.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace TakeInitiative.Api.Features.Users;

public class PostLogout(
    IOptions<JWTOptions> JWTOptions,
    UserManager<ApplicationUser> UserManager,
    SignInManager<ApplicationUser> SignInManager
    ) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/api/logout");
        AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
        Policies(TakePolicies.UserExists);
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            await CookieAuth.SignOutAsync();
        }
        catch (InvalidOperationException e)
        {
            ThrowError("User is not logged in", (int)HttpStatusCode.Unauthorized);
        }

        await SendOkAsync();
    }
}