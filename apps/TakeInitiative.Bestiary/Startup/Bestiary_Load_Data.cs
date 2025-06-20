using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using FastEndpoints;
using JasperFx;
using Marten;
using Marten.Services.Json;
using Newtonsoft.Json.Linq;
namespace BestiaryAPI.Startup
{
    //download, load and store data in marten db
    public static class Bestiary_Load_Data
    {
        static readonly HttpClient client = new HttpClient();
        const string download_url = "https://api.github.com/repos/5etools-mirror-3/5etools-src/releases/latest";

        static async Task download_5etools_data(IConfiguration config, bool IsDevelopment)
        {
            //HttpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("request");
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/111.0.0.0 Safari/537.36");
            //try
            //{

            //}
            //catch (HttpRequestException e)
            //{
            //    Console.WriteLine("\nException Caught!");
            //    Console.WriteLine("Message :{0} ", e.Message);
            //}
            string responseBody = await client.GetStringAsync(download_url);
            //Debug.WriteLine(responseBody);
            JObject jobject = JObject.Parse(responseBody);
            //Debug.WriteLine(jobject["zipball_url"]);
            var zipfile = "5etools.zip";
            using var downloadStream = await client.GetStreamAsync(jobject["zipball_url"].ToString());
            using var fileStream = new FileStream(zipfile, FileMode.Create, FileAccess.Write);

            await downloadStream.CopyToAsync(fileStream);
            await fileStream.FlushAsync();
            fileStream.Close();
            Debug.WriteLine("Done downloading");

            //check if existing folder exists and if so delete it
            if (Directory.Exists("5etools"))
            {
                Directory.Delete("5etools", true);
            }

            //unzip file
            ZipFile.ExtractToDirectory(zipfile, "5etools");
            Debug.WriteLine("Done extracting");
            //delete zip
            File.Delete(zipfile);
            Debug.WriteLine("Done deleting zipfile");

            string cwd = Directory.GetCurrentDirectory();
            //navigate to bestiary folder
            var intermediate_path = Path.Join(cwd, "5etools");
            //navigate again because the stupid folder is named something like 5etools-mirror-3-5etools-src-fe58f72
            //this sohuld only return 1 directory
            var dirs = Directory.GetDirectories(intermediate_path);
            //var bestiary_path = Path.Join(intermediate_path, dirs[0]);
            var bestiary_path = dirs[0];
            bestiary_path = Path.Join(bestiary_path, "data", "bestiary");
            Debug.WriteLine("Besteiary path is " + bestiary_path);
            var bestiary_files = Directory.GetFiles(bestiary_path);

            foreach (var bestiary_file in bestiary_files)
            {
                Debug.WriteLine("Processing file: " + bestiary_file);
            }

        }
    }
}
