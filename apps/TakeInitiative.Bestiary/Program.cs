using FastEndpoints;
using Marten;
using JasperFx;
using Marten.Services.Json;
using System.Threading.Tasks;

namespace BestiaryAPI;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddFastEndpoints();

        // Build config
        var configBuilder = builder.Configuration
            .AddJsonFile("appsettings.json", optional: false);
        if (!builder.Environment.IsProduction())
        {
            configBuilder = configBuilder.AddJsonFile("appsettings.development.json", optional: true);
        }

        var connstring = builder.Configuration.GetConnectionString("BestiaryDB");

        // This is the absolute, simplest way to integrate Marten into your
        // .NET application with Marten's default configuration
        builder.Services.AddMarten(options =>
        {
            // Establish the connection string to your Marten database
            options.Connection(builder.Configuration.GetConnectionString("BestiaryDB")!);

            // Specify that we want to use STJ as our serializer
            options.UseNewtonsoftForSerialization();

            // If we're running in development mode, let Marten just take care
            // of all necessary schema building and patching behind the scenes
            if (builder.Environment.IsDevelopment())
            {
                options.AutoCreateSchemaObjects = AutoCreate.All;
            }
        });
        //omg is this me using await and async??? Im a coding god now??
        await BestiaryAPI.Startup.Bestiary_Load_Data.download_5etools_data(builder.Configuration, builder.Environment.IsDevelopment());
        var app = builder.Build();
        app.UseDefaultExceptionHandler().UseFastEndpoints();
        app.Run();
    }
}

