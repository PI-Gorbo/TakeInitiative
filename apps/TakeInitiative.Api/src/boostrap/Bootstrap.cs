using FastEndpoints.Security;

using TakeInitiative.Api.Identity;

using Marten;
using Marten.Events.Daemon.Resiliency;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using SendGrid.Extensions.DependencyInjection;
using Serilog;
using TakeInitiative.Api.Features.Admin;
using TakeInitiative.Utilities;
using Weasel.Postgresql;

namespace TakeInitiative.Api.Bootstrap;
public static class Bootstrap
{
    public static IServiceCollection AddMartenDB(this IServiceCollection services, IConfiguration config, bool IsDevelopment)
    {
        var martenOpts = services.AddMarten(opts =>
        {
            
            opts.Connection(config.GetConnectionString("TakeDB") ?? throw new OperationCanceledException("Required Configuration 'ConnectionStrings:Marten' is missing."));

            // Use system.text.json            
            opts.UseSystemTextJsonForSerialization();

            // Registers the identity documents and enforces a unique index on NormalizedEmail.
            opts.RegisterIdentityModels<ApplicationUser, ApplicationUserRole>();
            
            opts.Schema.For<Campaign>()
                .Index(x => x.CampaignName);

            opts.Schema.For<CampaignMember>()
                .ForeignKey<ApplicationUser>(x => x.UserId, fk => fk.OnDelete = CascadeAction.Cascade)
                .ForeignKey<Campaign>(x => x.CampaignId, fk => fk.OnDelete = CascadeAction.Cascade);

            opts.Schema.For<IAdminConfig>()
                .AddSubClass<MaintenanceConfig>();
        }).AddAsyncDaemon(DaemonMode.Solo);

        if (IsDevelopment)
        {
            // Create the schema up front rather than leaning on Marten's implicit
            // auto-create, which makes a fresh database's behaviour depend on which
            // endpoint happens to be hit first. A schema conflict now fails startup
            // loudly; `docker compose -p takeinitiative -f compose.dev.yml down -v`
            // resets a stale local database.
            martenOpts.ApplyAllDatabaseChangesOnStartup();
        }

        martenOpts.UseLightweightSessions();

        return services;
    }

    public static WebApplicationBuilder AddIdentityAuthenticationAndAuthorization(this WebApplicationBuilder builder)
    {

        builder.Services
            .AddIdentityCore<ApplicationUser>(opts =>
            {
                opts.SignIn.RequireConfirmedAccount = false;
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
            .AddUserStore<MartenUserStore<ApplicationUser, ApplicationUserRole>>()
            .AddRoleStore<MartenRoleStore<ApplicationUserRole>>()
            .AddSignInManager() // Sign in manager allows users to sign in and out, and validates these operations.
            .AddDefaultTokenProviders(); // Default token providers for password changes and other temporary auth needs.

        builder.Services
            .AddTransient<IAuthorizationHandler, RequireUserToExistInDatabaseAuthorizationHandler>()
            .AddTransient<IAuthorizationHandler, RequireNotInMaintenanceModeAuthorizationHandler>()
            .AddCookieAuth(validFor: TimeSpan.FromHours(24), opts =>
            {
                opts.SlidingExpiration = true; // Reissue new cookies when the cookie is half or more through its timespan.

                opts.Events.OnRedirectToLogin = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };

                opts.Events.OnRedirectToAccessDenied = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                opts.Cookie.Domain = builder.Configuration.GetValue<string>("CookieDomain") ?? throw new InvalidOperationException("Attempted to find configuration for the value CookieDomain but there was none provided.");

                opts.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
            })
            .AddAuthorization(opts =>
            {
                opts.AddPolicy(TakePolicies.UserExists,
                    new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser() // Requires JWT to exist and be signed by the api
                        .AddRequirements(new RequireUserToExistInDatabaseAuthorizationRequirement())
                        .Build()
                );

                opts.AddPolicy(TakePolicies.NotInMaintenanceMode,
                    new AuthorizationPolicyBuilder()
                        .AddRequirements(new RequireNotInMaintenanceModeAuthorizationRequirement())
                        .Build()
                );
            })
            .AddSingleton<IAuthorizationMiddlewareResultHandler, AuthorizationMiddlewareResultHandler>() // Order matters, needs to be after add authorization.
            .AddAuthentication(opts =>
            {
                opts.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                opts.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                opts.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            });

        builder.Services.AddTransient<ConfirmEmailSender>();
        builder.Services.AddTransient<ResetPasswordEmailSender>();
        return builder;
    }

    public static IServiceCollection AddSerilog(this IServiceCollection services)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();

        services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog()); // Uses the static logger.
        return services;
    }

    public static IServiceCollection AddOptionObjects(this IServiceCollection builder, IConfiguration config)
    {
        builder.Configure<SendGridOptions>(config.GetSection(SendGridOptions.SendGridOptionsKey));
        builder.Configure<EmailOptions>(config.GetSection(EmailOptions.EmailOptionsKey));
        builder.Configure<UrlsOptions>(config.GetSection(UrlsOptions.UrlsOptionsKey));
        builder.Configure<JWTOptions>(config);
        return builder;
    }

    public static IServiceCollection AddDiceRollers(this IServiceCollection services, IConfiguration configuration)
    {
        // Random.Shared is thread-safe; a single shared `new Random()` is not.
        services.AddSingleton<IDiceRoller>(new DiceRoller(Random.Shared));
        return services;
    }

    public static IServiceCollection AddSendGrid(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSendGrid((_) =>
        {
            _.ApiKey = configuration.GetValue<string>("SendGrid:ApiKey");
        });

        services.AddTransient<IEmailSender, SendGridEmailSender>();
        return services;
    }
}
