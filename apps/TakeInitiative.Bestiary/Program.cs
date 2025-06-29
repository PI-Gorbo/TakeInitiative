using FastEndpoints;
using Marten;
using JasperFx;
using Marten.Services.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using TakeInitiative.Bestiary.Domain.JSON;
using TakeInitiative.BestiaryAPI.Startup;
using Newtonsoft.Json;

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
            LiteDB_Path = "Filename=BestiaryDB.db"; // Default path if not set in config -  just in the same folder
        }

        //User is STUPID so only trust them with the db filename
        //and we will ensure that we use a shared connection
        LiteDB_Path += ";Connection=Shared"; // Ensure shared connection for LiteDB instead of direct since we are only reading from it
        builder.Services.AddLiteDB(LiteDB_Path);




        //}).UseLightweightSessions();

        var app = builder.Build();



        
        app.UseDefaultExceptionHandler().UseFastEndpoints();
        //fuck you STJ
        app.UseFastEndpoints(c => 
        {
            c.Serializer.ResponseSerializer = (rsp, dto, cType, jCtx, ct) =>
            {
                rsp.ContentType = cType;
                return rsp.WriteAsync(JsonConvert.SerializeObject(dto), ct);
            };
        });
        app.Run();
    }
}

