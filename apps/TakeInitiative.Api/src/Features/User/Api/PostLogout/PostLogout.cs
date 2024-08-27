using System.Net;
using FastEndpoints;
using FastEndpoints.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace TakeInitiative.Api.Features.Users;

public class PostLogout() : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/api/logout");
    }
    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            await CookieAuth.SignOutAsync();
        }
        catch
        {
            ThrowError("User is not logged in", (int)HttpStatusCode.Unauthorized);
        }

        await SendOkAsync();
    }
}