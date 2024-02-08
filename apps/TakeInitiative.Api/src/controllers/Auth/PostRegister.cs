using System.Net;
using FastEndpoints;
using FastEndpoints.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using TakeInitiative.Api.Bootstrap;
using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Controllers;

public class PostRegister(
	IOptions<JWTOptions> JWTOptions,
	UserManager<ApplicationUser> UserManager,
	SignInManager<ApplicationUser> SignInManager
	) : Endpoint<PostRegisterRequest, PostRegisterResponse>
{
	public override void Configure()
	{
		Post("/api/register");
		AllowAnonymous();
	}
	public override async Task HandleAsync(PostRegisterRequest req, CancellationToken ct)
	{
		if (await UserManager.FindByEmailAsync(req.Email) != null)
		{
			AddError("This email is already registered to another account.");
		}

		if (await UserManager.FindByNameAsync(req.Username) != null)
		{
			AddError("This username is already registered to another account.");
		}

		ThrowIfAnyErrors();

		var user = new ApplicationUser();
		await UserManager.SetUserNameAsync(user, req.Username);
		await UserManager.SetEmailAsync(user, req.Email);
		var result = await UserManager.CreateAsync(user, req.Password);

		if (!result.Succeeded)
		{
			ThrowError($"Failed to register new account! {String.Join(", ", result.Errors.Select(x => x.Description))}", (int)HttpStatusCode.BadRequest);
		}

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