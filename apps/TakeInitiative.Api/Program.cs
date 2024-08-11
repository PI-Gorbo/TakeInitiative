using System.Net;
using System.Text.Json;
using FastEndpoints;
using FastEndpoints.Swagger;
using JasperFx.Core;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
namespace TakeInitiative.Api;
internal class Program
{
    private static void Main(string[] args)
    {
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
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddHealthChecks();
        builder.Services.AddFastEndpoints();
        builder.Services.AddSwaggerGen(genOptions =>
        {
            genOptions.UseInlineDefinitionsForEnums();
        });
        builder.Services.AddSignalR();

        // Dev only
        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddOpenApiDocument(doc => doc.DocumentName = "TakeInitiativeApi");
            builder.Services.SwaggerDocument(); //define a swagger document;
        }

        // Custom Injection
        builder.Services.AddOptionObjects(builder.Configuration);
        builder.Services.AddMartenDB(builder.Configuration, builder.Environment.IsDevelopment());
        builder.Services.AddSerilog();
        builder.Services.AddIdentityAuthenticationAndAuthorization(builder.Configuration);
        builder.Services.AddPython(builder.Configuration);
        builder.Services.AddSendGrid(builder.Configuration);

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

        app.MapHub<CombatHub>("/combatHub");
        app.MapHub<CampaignHub>("/campaignHub");

        app.UseCors("MainAppCors")
            .UseFastEndpoints(cfg =>
            {
                cfg.Endpoints.Configurator = (endpoint) =>
                {
                    // Set the auth scheme for the endpoints
                    if (endpoint.Routes?.Any(route => route.StartsWith("/api/admin")) ?? false) // All the endpoints that start with /api/admin must only be accessed by the admin portal.
                    {
                        endpoint.Options(opts => opts.RequireCors("AdminAppCors"));
                        endpoint.AllowAnonymous(["GET", "POST", "PUT", "DELETE"]);
                    }
                    else if (endpoint.EndpointTags?.Any(tag => tag == "AllowAnonymous") ?? false) // Endpoints must opt into the AllowAnonymous.
                    {
                        endpoint.AllowAnonymous();
                    }
                    else // Otherwise default auth policies are required.
                    {
                        endpoint.AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
                        endpoint.Policies(TakePolicies.NotInMaintenanceMode, TakePolicies.UserExists);
                    }

                    // // Swagger fixes.
                    var endpointType = endpoint.EndpointType;
                    if (endpointType.BaseType != null && endpointType.BaseType.IsGenericType && endpointType.BaseType?.GetGenericTypeDefinition() == typeof(Endpoint<>))
                    {
                        endpoint.Summary(summary => summary.Response((int)HttpStatusCode.OK));
                    }

                };
            })
            .UseAuthentication()
            .UseAuthorization();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwaggerGen();
        }

        app.UseHealthChecks("/healthz");

        app.UseSwaggerGen();
        app.Run();
    }
}