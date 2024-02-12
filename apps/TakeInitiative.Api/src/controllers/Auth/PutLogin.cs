using System.Net;
using FastEndpoints;
using FastEndpoints.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using TakeInitiative.Api.Bootstrap;
using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Controllers;
public class PutLogin(
	IOptions<JWTOptions> JWTOptions,
	UserManager<ApplicationUser> UserManager,
	SignInManager<ApplicationUser> SignInManager) : Endpoint<PutLoginRequest, PutLoginResponse>
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

		await CookieAuth.SignInAsync(u =>
		{
			//indexer based claim setting
			u["UserId"] = user.Id.ToString();
		});


		var jwtToken = JWTBearer.CreateToken(
			JWTOptions.Value.JWTSigningKey,
			expireAt: DateTime.UtcNow.AddDays(7),
			privileges: u =>
			{
				u["UserId"] = user.Id.ToString();
			});
		await SendAsync(new()
		{
			Token = jwtToken,
		});
	}
}