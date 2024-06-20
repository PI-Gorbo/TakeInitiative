using System.Text.Json;
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

            opts.Schema.For<IAdminConfig>()
                .AddSubClass<MaintenanceConfig>();

            // Event Projections
            opts.Projections
                .Add(new CombatProjection(), ProjectionLifecycle.Inline, null);


        }).AddAsyncDaemon(DaemonMode.Solo);

        if (IsDevelopment)
        {
            martenOpts.ApplyAllDatabaseChangesOnStartup();
        }

        martenOpts.UseLightweightSessions();

        return services;
    }

    public static IServiceCollection AddIdentityAuthenticationAndAuthorization(this IServiceCollection services, IConfiguration config)
    {
        services
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
            .AddUserStore<MartenUserStore>()
            .AddRoleStore<MartenRoleStore>()
            .AddSignInManager() // Sign in manager allows users to sign in and out, and validates these operations.
            .AddDefaultTokenProviders(); // Default token providers for password changes and other temporary auth needs.

        services
            .AddTransient<IAuthorizationHandler, RequireUserToExistInDatabaseAuthorizationHandler>()
            .AddTransient<IAuthorizationHandler, RequireNotInMaintenanceModeAuthorizationHandler>()
            .AddCookieAuth(validFor: TimeSpan.FromHours(1), opts =>
            {
                opts.SlidingExpiration = true; // Reissue new cookies when the cookie is half or more through its timespan.

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
                opts.Cookie.Domain = config.GetValue<string>("CookieDomain") ?? throw new InvalidOperationException("Attempted to find configuration for the value CookieDomain but there was none provided.");
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

        services.AddTransient<ConfirmEmailSender>();
        services.AddTransient<ResetPasswordEmailSender>();
        return services;
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

    public static IServiceCollection AddPython(this IServiceCollection services, IConfiguration configuration)
    {
        var pythonConfig = configuration.GetValue<string>("PythonDLL") ?? throw new InvalidConfigurationException("There is no configuration value for PythonDLL. Please set a value.");
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
        services.AddTransient<IDiceRoller, DiceRoller>();
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
