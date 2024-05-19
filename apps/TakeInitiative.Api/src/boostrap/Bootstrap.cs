using CSharpFunctionalExtensions;
using FastEndpoints.Security;
using Marten;
using Marten.Events.Daemon.Resiliency;
using Marten.Events.Projections;
using Marten.Services.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Protocols.Configuration;
using Python.Runtime;
using Serilog;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;
using Weasel.Postgresql;

namespace TakeInitiative.Api.Bootstrap;
public static class Bootstrap
{
    public static WebApplicationBuilder AddMartenDB(this WebApplicationBuilder builder)
    {

        var martenOpts = builder.Services.AddMarten(opts =>
        {
            opts.Connection(builder.Configuration.GetConnectionString("TakeDB") ?? throw new OperationCanceledException("Required Configuration 'ConnectionStrings:Marten' is missing."));

            // Use system.text.json
            opts.UseDefaultSerialization(serializerType: SerializerType.SystemTextJson);

            opts.Schema.For<ApplicationUser>();
            opts.Schema.For<ApplicationUserRole>();
            opts.Schema.For<Campaign>()
                .Index(x => x.CampaignName);

            opts.Schema.For<CampaignMember>()
                .ForeignKey<ApplicationUser>(x => x.UserId, fk => fk.OnDelete = CascadeAction.Cascade)
                .ForeignKey<Campaign>(x => x.CampaignId, fk => fk.OnDelete = CascadeAction.Cascade);

            opts.Schema.For<PlannedCombat>()
                .Index(x => x.CombatName)
                .ForeignKey<Campaign>(x => x.CampaignId, fk => fk.OnDelete = CascadeAction.Cascade);

            // Event Projections
            opts.Projections
                .Add(new CombatProjection(), ProjectionLifecycle.Inline, null);


        }).AddAsyncDaemon(DaemonMode.Solo);

        if (builder.Environment.IsDevelopment())
        {
            martenOpts.ApplyAllDatabaseChangesOnStartup();
        }

        martenOpts.UseLightweightSessions();

        return builder;
    }

    public static WebApplicationBuilder AddIdentityAuthenticationAndAuthorization(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddIdentityCore<ApplicationUser>(opts =>
            {
                opts.Password = new PasswordOptions()
                {
                    RequireDigit = true,
                    RequiredLength = 6,
                    RequireLowercase = true,
                    RequireUppercase = true,
                    RequireNonAlphanumeric = true
                };
            })
            .AddRoles<ApplicationUserRole>()
            .AddUserStore<MartenUserStore>()
            .AddRoleStore<MartenRoleStore>()
            .AddSignInManager() // Sign in manager allows users to sign in and out, and validates these operations.
            .AddDefaultTokenProviders(); // Default token providers for password changes and other temporary auth needs.

        builder.Services
            .AddSingleton<IAuthorizationHandler, RequireUserToExistInDatabaseAuthorizationHandler>()
            .AddCookieAuth(validFor: TimeSpan.FromHours(1), opts =>
            {
                opts.Events.OnRedirectToLogin = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };

                opts.Events.OnRedirectToAccessDenied = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            })
            .AddAuthorization(opts =>
            {
                opts.AddPolicy("UserExists",
                    new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser() // Requires JWT to exist and be signed by the api
                    .AddRequirements(new RequireUserToExistInDatabaseAuthorizationRequirement())
                    .Build()
                );
            })
            .AddAuthentication(opts =>
            {
                opts.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                opts.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                opts.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            });
        return builder;
    }

    public static WebApplicationBuilder AddSerilog(this WebApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();

        builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog()); // Uses the static logger.
        return builder;
    }

    public static WebApplicationBuilder AddOptionObjects(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<JWTOptions>(builder.Configuration);
        return builder;
    }

    public static WebApplicationBuilder AddPython(this WebApplicationBuilder builder)
    {
        var pythonConfig = builder.Configuration.GetValue<string>("PythonDLL") ?? throw new InvalidConfigurationException("There is no configuration value for PythonDLL. Please set a value.");
        if (pythonConfig == "null")
        {
            // /usr/lib/python3.11/config-3.11-x86_64-linux-gnu/libpython3.11.so
            Log.Information("Identified PythonDLL path as null and attempting to identify .so location...");
            var configName = new DirectoryInfo("/usr/lib/python3.11")
                .EnumerateDirectories("config-3.11-*")
                .First();

            pythonConfig = configName.FullName + "/libpython3.11.so";
            Log.Information($"Found! {pythonConfig}");
        }


        Runtime.PythonDLL = pythonConfig;
        PythonEngine.Initialize();
        PythonEngine.BeginAllowThreads();

        // Add dice roller.
        builder.Services.AddTransient<IDiceRoller, DiceRoller>();
        return builder;
    }

}

public static class TakePolicies
{
    public static string UserExists = "UserExists";
}
public class RequireUserToExistInDatabaseAuthorizationRequirement : IAuthorizationRequirement { }
public class RequireUserToExistInDatabaseAuthorizationHandler(IDocumentStore Store) : AuthorizationHandler<RequireUserToExistInDatabaseAuthorizationRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, RequireUserToExistInDatabaseAuthorizationRequirement requirement)
    {
        // Validate the user's existence in the database.
        Result<bool> userExistsResult = await Result.SuccessIf(
            Guid.TryParse(context.User.Claims.SingleOrDefault(x => x.Type == "UserId")?.Value, out Guid parsedValue),
            parsedValue,
            "Failed to parse UserId in claims as Guid.")
        .Bind((id) => Store.Try(async (session) =>
        {
            return await session.Query<ApplicationUser>().AnyAsync(x => x.Id == id);
        })).Ensure(userExists =>
        {
            return userExists;
        }, "User does not exist.");

        if (userExistsResult.IsFailure)
        {
            context.Fail(new AuthorizationFailureReason(this, userExistsResult.Error));
            return;
        }

        context.Succeed(requirement);
    }
}