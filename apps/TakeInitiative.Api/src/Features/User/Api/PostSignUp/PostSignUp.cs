using System.Net;
using FastEndpoints;
using FastEndpoints.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace TakeInitiative.Api.Features.Users;

public class PostSignUp(
    IOptions<JWTOptions> JWTOptions,
    UserManager<ApplicationUser> UserManager,
    SignInManager<ApplicationUser> SignInManager
    ) : Endpoint<PostSignUpRequest>
{
    public override void Configure()
    {
        Post("/api/signup");
        AllowAnonymous();
    }
    public override async Task HandleAsync(PostSignUpRequest req, CancellationToken ct)
    {
        var user = new ApplicationUser();
        if (await UserManager.FindByEmailAsync(req.Email) != null)
        {
            AddError((req) => req.Email, "This email is already registered to another account.");
        }

        if (await UserManager.FindByNameAsync(req.Username) != null)
        {
            AddError((req) => req.Username, "This username is already registered to another account.");
        }

        // Check the users password by applying the password validators.
        foreach (IPasswordValidator<ApplicationUser> validator in UserManager.PasswordValidators)
        {
            var validationResult = await validator.ValidateAsync(UserManager, user, req.Password);
            if (!validationResult.Succeeded)
            {
                foreach (var error in validationResult.Errors)
                {
                    AddError(req => req.Password, error.Description);
                }
            }
        }

        ThrowIfAnyErrors();
        await UserManager.SetEmailAsync(user, req.Email);
        await UserManager.SetUserNameAsync(user, req.Username);
        var result = await UserManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
        {
            ThrowError($"Failed to register new account! {String.Join(", ", result.Errors.Select(x => x.Description))}", (int)HttpStatusCode.BadRequest);
        }

        await CookieAuth.SignInAsync(u =>
        {
            //indexer based claim setting
            u["UserId"] = user.Id.ToString();
        });

        await SendOkAsync(ct);
    }
}