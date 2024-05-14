using FastEndpoints;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace TakeInitiative.Api.Features.Users;
public class GetAuthenticated(
    IOptions<JWTOptions> JWTOptions,
    UserManager<ApplicationUser> UserManager,
    SignInManager<ApplicationUser> SignInManager) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/api/authenticated");
        AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
        Policies(TakePolicies.UserExists);
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        await SendOkAsync(ct);
    }
}