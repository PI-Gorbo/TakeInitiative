using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Text.Json.Serialization;
using NSwag.Generation;

namespace TakeInitiative.Api;
internal class Program
{
    private const string OpenApiDocumentName = "v1";

    private static async Task Main(string[] args)
    {
        // `dotnet run -- --export-openapi <path>` writes the OpenAPI document and exits.
        // The host is never started, so no hosted service (Marten's schema migration
        // included) runs and no database is needed.
        var exportOpenApiPath = ExportOpenApiPath(args);

        var builder = WebApplication.CreateBuilder(args);

        // Build config
        var configBuilder = builder.Configuration
            .AddJsonFile("appsettings.json", optional: false);
        if (!builder.Environment.IsProduction())
        {
            configBuilder = configBuilder.AddJsonFile("appsettings.development.json", optional: true);
        }
        configBuilder.AddEnvironmentVariables();

        // Add services to the container.
        builder.Services.SwaggerDocument(opts =>
        {
            opts.DocumentSettings = s =>
            {
                s.DocumentName = OpenApiDocumentName;
                s.Title = "TakeInitiative API";
                s.Version = "v1";
                // Non-nullable properties are required, so the generated TypeScript
                // types only mark genuinely nullable fields as optional.
                s.MarkNonNullablePropsAsRequired();
            };
            // Role and friends go over the wire as strings (see Serializer below), so
            // the document describes them as string enums too.
            opts.SerializerSettings = s => s.Converters.Add(new JsonStringEnumConverter());
            opts.ShortSchemaNames = true;
            opts.ExcludeNonFastEndpoints = true;
            opts.AutoTagPathSegmentIndex = 2;
        });
        builder.Services.AddHealthChecks();
        builder.Services.AddFastEndpoints();
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<CampaignConnections>();
        // The gap prompt's clock. Keyed so tests can move it without also moving the
        // clock cookie authentication reads (an unkeyed TimeProvider), which would expire
        // the test users' cookies.
        builder.Services.AddKeyedSingleton(SessionGap.ClockKey, TimeProvider.System);

        // Custom Injection
        builder.Services.AddOptionObjects(builder.Configuration);
        builder.Services.AddMartenDB(builder.Configuration, builder.Environment.IsDevelopment());
        builder.Services.AddSerilog();
        builder.AddIdentityAuthenticationAndAuthorization();
        builder.Services.AddDiceRollers(builder.Configuration);
        builder.Services.AddSendGrid(builder.Configuration);
        builder.Services.AddImages(builder.Configuration);

        // Cors
        builder.Services.AddCors(
            opts =>
            {
                var mainAppCors = (builder.Configuration.GetValue<string>("CORS:MainApp") ?? throw new MissingMemberException("Missing configuration for value 'CORS:MainApp'.")).Split(";").ToArray();
                opts.AddPolicy("MainAppCors", corsBuilder => corsBuilder
                                .WithOrigins(mainAppCors)
                                .AllowAnyHeader()
                                .AllowAnyMethod()
                                .AllowCredentials());

                var adminAppCors = (builder.Configuration.GetValue<string>("CORS:AdminApp") ?? throw new MissingMemberException("Missing configuration for value 'CORS:AdminApp'.")).Split(";").ToArray();
                opts.AddPolicy("AdminAppCors", corsBuilder => corsBuilder
                                .WithOrigins(adminAppCors)
                                .AllowAnyHeader()
                                .AllowAnyMethod()
                                .AllowCredentials());

                opts.AddPolicy("MainAppAndAdminApp", corsBuilder => corsBuilder
                                .WithOrigins([.. mainAppCors, .. adminAppCors])
                                .AllowAnyHeader()
                                .AllowAnyMethod()
                                .AllowCredentials());
            });
        
        var app = builder.Build();

        // Map SignalR Hubs
        app.MapHub<CampaignHub>("/campaignHub");

        app
            .UseCors("MainAppCors")
            .UseMiddleware<CorrelationMiddleware>()
            .UseFastEndpoints(cfg =>
            {
                // Operation ids are the endpoint class names (GetCampaign, not
                // TakeInitiativeApiFeaturesCampaignsGetCampaign).
                cfg.Endpoints.ShortNames = true;
                cfg.Serializer.Options.Converters.Add(new JsonStringEnumConverter());
                cfg.Endpoints.Configurator = (endpoint) =>
                {
                    if (endpoint.Routes?.Any(route => route.StartsWith("/api/admin")) ?? false)
                    {
                        endpoint.Options(opts => opts.RequireCors("AdminAppCors"));
                        endpoint.AllowAnonymous(["GET", "POST", "PUT", "DELETE"]);
                    }
                    else if (endpoint.EndpointTags?.Any(tag => tag == "AllowAnonymous") ?? false)
                    {
                        endpoint.AllowAnonymous();
                    }
                    else
                    {
                        endpoint.AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
                        endpoint.Policies(TakePolicies.NotInMaintenanceMode, TakePolicies.UserExists);
                    }
                };

                // cfg.Serializer.Options.TypeInfoResolverChain.Add(new PolymorphicTypeResolver());
            })
            .UseAuthentication()
            .UseAuthorization();

        // Must come after UseFastEndpoints, which sets up the resolver the document generator uses.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwaggerGen();
        }

        app.UseHealthChecks("/healthz");

        if (exportOpenApiPath is not null)
        {
            await ExportOpenApi(app, exportOpenApiPath);
            return;
        }

        await app.RunAsync();
    }

    private static string? ExportOpenApiPath(string[] args)
    {
        var index = Array.IndexOf(args, "--export-openapi");
        if (index < 0)
        {
            return null;
        }
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException("--export-openapi needs a destination path.");
        }
        return Path.GetFullPath(args[index + 1]);
    }

    private static async Task ExportOpenApi(WebApplication app, string path)
    {
        // WebApplication only hands its endpoints to the API explorer when the host
        // starts. Wire them up by hand instead, since starting would also run the
        // hosted services. The pipeline is never served, so middleware order is moot.
        app.UseRouting();
        app.UseEndpoints(_ => { });

        var generator = app.Services.GetRequiredService<IOpenApiDocumentGenerator>();
        var document = await generator.GenerateAsync(OpenApiDocumentName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, document.ToJson() + "\n");
        Console.WriteLine($"Wrote OpenAPI document to {path}");
    }
}