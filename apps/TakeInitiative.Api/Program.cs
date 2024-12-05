using FastEndpoints;
using Microsoft.AspNetCore.Authentication.Cookies;

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
        builder.Services.AddSwaggerGen();
        builder.Services.AddHealthChecks();
        builder.Services.AddFastEndpoints();
        builder.Services.AddSignalR();

        // Custom Injection
        builder.Services.AddOptionObjects(builder.Configuration);
        builder.Services.AddMartenDB(builder.Configuration, builder.Environment.IsDevelopment());
        builder.Services.AddSerilog();
        builder.AddIdentityAuthenticationAndAuthorization();
        builder.Services.AddDiceRollers(builder.Configuration);
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

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // Map SignalR Hubs
        app.MapHub<CombatHub>("/combatHub");
        app.MapHub<CampaignHub>("/campaignHub");

        app
            .UseCors("MainAppCors")
            .UseFastEndpoints(cfg =>
            {
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

        app.UseHealthChecks("/healthz");
        app.Run();
    }
}