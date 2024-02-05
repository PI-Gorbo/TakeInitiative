using System.Security.Claims;
using CSharpFunctionalExtensions;
using FastEndpoints.Security;
using Marten;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using TakeInitiative.Api.Bootstrap;
using TakeInitiative.Api.Models;
using TakeInitiative.Setup.Database;
using TakeInitiative.Utilities.Extensions;

public static class Bootstrap {

	public static WebApplicationBuilder AddMartenDB(this WebApplicationBuilder builder) {

		var martenOpts = builder.Services.AddMarten(opts => {
			opts.Connection(builder.Configuration.GetConnectionString("TakeDB") ?? throw new OperationCanceledException("Required Configuration 'ConnectionStrings:Marten' is missing."));

			opts.Schema.For<ApplicationUser>();
			opts.Schema.For<ApplicationUserRole>();
		});

		if (builder.Environment.IsDevelopment()) {
			martenOpts.ApplyAllDatabaseChangesOnStartup();
		}

		return builder;
	}	

	public static WebApplicationBuilder AddIdentityAuthenticationAndAuthorization(this WebApplicationBuilder builder ) {
		builder.Services
			.AddIdentity<ApplicationUser, ApplicationUserRole>()
			.AddUserStore<MartenUserStore>()
			.AddRoleStore<MartenRoleStore>()
			.AddSignInManager() // Sign in manager allows users to sign in and out, and validates these operations.
			.AddDefaultTokenProviders(); // Default token providers for password changes and other temporary auth needs.

		builder.Services
			.AddSingleton<IAuthorizationHandler, RequireUserToExistInDatabaseAuthorizationHandler>()
			.AddAuthorization(opts => {
				opts.AddPolicy("ValidUser", 
					new AuthorizationPolicyBuilder()
					.RequireAuthenticatedUser() // Requires JWT to exist and be signed by the api
					.AddRequirements(new RequireUserToExistInDatabaseAuthorizationRequirement())
					.Build()
				); 
			})
			.AddJWTBearerAuth(builder.Configuration.GetValue<string>("JWTSigningKey") ?? throw new OperationCanceledException("Required configuration 'JWTSigningKey' is missing"))
			.AddAuthentication(opts =>
			{
				
			});
		return builder;
	}

	public static WebApplicationBuilder AddOptionObjects(this WebApplicationBuilder builder) {
		builder.Services.Configure<JWTOptions>(builder.Configuration);
		return builder;
	}

}

public static class TakePolicies {
	public static string ValidUser = "ValidUser";
}
public class RequireUserToExistInDatabaseAuthorizationRequirement : IAuthorizationRequirement {}
public class RequireUserToExistInDatabaseAuthorizationHandler(IDocumentStore Store) : AuthorizationHandler<RequireUserToExistInDatabaseAuthorizationRequirement>
{
	protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, RequireUserToExistInDatabaseAuthorizationRequirement requirement)
	{
		// Validate the user's existence in the database.
		var userExistsResult = await Result.SuccessIf(
			Guid.TryParse(context.User.Claims.Single(x => x.Type == "UserId").Value, out Guid parsedValue), 
			parsedValue, 
			"Failed to parse UserId in Claims as Guid.")
		.Bind((id) => Store.Try(async (session) => {
			return await session.LoadAsync<ApplicationUser>(id);
		})).EnsureNotNull("The user was not found in the database.");
		
		if (userExistsResult.IsFailure) {
			context.Fail(new AuthorizationFailureReason(this, userExistsResult.Error));
			return;
		}

		context.Succeed(requirement);
	}
}