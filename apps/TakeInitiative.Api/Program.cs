using FastEndpoints;
using Marten;
using Microsoft.Net.Http.Headers;
using Serilog;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddHealthChecks();
        builder.Services.AddFastEndpoints();
        builder.Services.AddMvc(options => options.EnableEndpointRouting = false);

        // Custom Injection
        builder.AddMartenDB();
        builder.AddSerilog();
        builder.AddIdentityAuthenticationAndAuthorization();
        builder.AddOptionObjects();

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

        app.UseCors("ApiCORS")
            .UseFastEndpoints()
            .UseAuthentication()
            .UseAuthorization();


        app.UseHealthChecks("/healthz");
        app.Run();
    }
}