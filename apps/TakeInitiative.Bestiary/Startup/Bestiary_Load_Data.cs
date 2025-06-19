using FastEndpoints;
using Marten;
using JasperFx;
using Marten.Services.Json;
using System;
using System.IO;
namespace BestiaryAPI.Startup
{
    //download, load and store data in marten db
    public static class Bestiary_Load_Data
    {
        const string download_url = "https://api.github.com/repos/5etools-mirror-3/5etools-src/releases/latest";
        public static void LoadData(IConfiguration config, bool IsDevelopment)
        {
            
        }
    }
}
