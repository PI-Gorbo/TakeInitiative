using System.Net;
using FastEndpoints;
using FastEndpoints.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using TakeInitiative.Api.Bootstrap;
using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Controllers;
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