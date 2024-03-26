using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Marten;
using Microsoft.Extensions.Configuration;
internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddMarten(options => {
            options.Connection(builder.Configuration.GetConnectionString("BeastDB") ?? throw new OperationCanceledException("Required Configuration 'ConnectionStrings:BeastDB' is missing."));
        });
        var app = builder.Build();

        app.MapGet("/", () => "Hello World!");
        app.Run();
    }
}