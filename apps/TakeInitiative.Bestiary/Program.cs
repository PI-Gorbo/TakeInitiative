using FastEndpoints;
using Marten;
using JasperFx;
using Marten.Services.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using TakeInitiative.Bestiary.Domain.JSON;
using TakeInitiative.BestiaryAPI.Startup;

namespace TakeInitiative.BestiaryAPI;

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
            Debug.WriteLine("Loading dev configuration");
            configBuilder = configBuilder.AddJsonFile("appsettings.development.json", optional: true);
        }
        

        var LiteDB_Path = builder.Configuration.GetConnectionString("Bestiary_LiteDB");
        if (LiteDB_Path == null || LiteDB_Path == "") {
            LiteDB_Path = "BestiaryDB.db"; // Default path if not set in config -  just in the same folder
        }

        LiteDB_Path += ";Connection=Shared"; // Ensure shared connection for LiteDB instead of direct since we are only reading from it
        builder.Services.AddLiteDB(LiteDB_Path);




        //}).UseLightweightSessions();

        var app = builder.Build();



        
        app.UseDefaultExceptionHandler().UseFastEndpoints();
        app.Run();
    }
}

