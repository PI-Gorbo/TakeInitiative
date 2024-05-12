using FastEndpoints;
using Microsoft.AspNetCore.Mvc;
using Python.Runtime;
using TakeInitiative.Api.Features;
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
        builder.AddMartenDB();
        builder.AddSerilog();
        builder.AddIdentityAuthenticationAndAuthorization();
        builder.AddOptionObjects();
        builder.AddPython();

        // Cors
        var cors = (builder.Configuration.GetValue<string>("CORS") ?? throw new MissingMemberException("Missing configuration for value 'CORS'."))
                        .Split(";").ToArray();

        builder.Services.AddCors(opts => opts.AddPolicy("ApiCORS", corsBuilder => corsBuilder
                .WithOrigins(cors)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
            ));


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

        app.UseCors("ApiCORS")
            .UseFastEndpoints()
            .UseAuthentication()
            .UseAuthorization();

        app.UseHealthChecks("/healthz");
        app.MapGet("/devInfo", ([FromServices] IConfiguration config) => ((IConfigurationRoot)config).GetDebugView());
        app.Run();
    }
}