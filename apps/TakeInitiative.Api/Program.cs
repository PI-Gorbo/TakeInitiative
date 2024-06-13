using System.Text.Json;
using FastEndpoints;
using JasperFx.Core;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
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
        builder.Services.AddSwaggerGen();
        builder.Services.AddHealthChecks();
        builder.Services.AddFastEndpoints();
        builder.Services.AddSignalR();

        // Custom Injection
        builder.AddOptionObjects();
        builder.AddMartenDB();
        builder.AddSerilog();
        builder.AddIdentityAuthenticationAndAuthorization();
        builder.AddPython();
        builder.AddSendGrid();

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
            });

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // Map SignalR Hubs
        app.MapHub<CombatHub>("/combatHub");
        app.MapHub<CampaignHub>("/campaignHub");

        app.UseCors("MainAppCors")
            .UseFastEndpoints(cfg =>
            {
                cfg.Endpoints.Configurator = (endpoint) =>
                {
                    if (endpoint.Routes.Any(route => route.StartsWith("/api/admin")))
                    {
                        endpoint.Options(opts => opts.RequireCors("AdminAppCors"));
                    }
                    else
                    {
                        endpoint.AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
                        endpoint.Policies(TakePolicies.UserExists);
                    }
                };
            })
            .UseAuthentication()
            .UseAuthorization();

        app.UseHealthChecks("/healthz");
        app.Run();
    }
}