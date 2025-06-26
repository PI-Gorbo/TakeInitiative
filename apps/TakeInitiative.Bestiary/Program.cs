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

        var connstring = builder.Configuration.GetConnectionString("BestiaryDB") ?? throw new OperationCanceledException("Required Config 'ConnectionStrings:BestiaryDB' is missing.");

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
            //5etools link as ID is jank but its a combo of name + source which should work when given cringe name duplicates
            options.Schema.For<StopGapMonsterClass>()
                //.Identity(x => x.Name)
                .Identity(x => x._5etools_link)
                .Index(x => x.Source)
                .Duplicate(x => x.Name); 


        }).UseLightweightSessions();

        var app = builder.Build();

        // Thanky Sam!!
        var store = app.Services.GetRequiredService<IDocumentStore>();
        if (store == null)
        {
            throw new Exception("Bruh wtf why is store null??? This should never be null");
        }

        //omg is this me using await and async??? Im a coding god now?? I definitely know what im doing and how they work????!!!
        await Bestiary_Load_Data.download_5etools_data(store);

        app.UseDefaultExceptionHandler().UseFastEndpoints();
        app.Run();
    }
}

