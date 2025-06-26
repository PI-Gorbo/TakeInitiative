using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using FastEndpoints;
using JasperFx;
using Marten;
using Marten.Services.Json;
using Newtonsoft.Json.Linq;
using TakeInitiative.BestiaryAPI;
using Newtonsoft.Json;
using TakeInitiative.Bestiary.Domain.JSON;

namespace TakeInitiative.BestiaryAPI.Startup
{
    //download, load and store data in marten db
    public static class Bestiary_Load_Data
    {
        static readonly HttpClient client = new HttpClient();
        const string download_url = "https://api.github.com/repos/5etools-mirror-3/5etools-src/releases/latest";

        public static async Task download_5etools_data(IDocumentStore store)
        {
            var monsters = await Download_Data_SrcGithub();
            Debug.WriteLine("Downloaded {0} monsters from 5etools", monsters.Count);
            //insert data into marten db
            await Insert_Data_marten(store, monsters);
            //clean up
            Debug.WriteLine("Done inserting {0} monsters into DB", monsters.Count());
            //Clean up 5etools folder, delete it
            if (Directory.Exists("5etools"))
            {
                Directory.Delete("5etools", true);
            }
        }


        public static async Task<List<StopGapMonsterClass>> Download_Data_SrcGithub()
        {

            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/111.0.0.0 Safari/537.36");

            string responseBody = await client.GetStringAsync(download_url);

            JObject jobject = JObject.Parse(responseBody);

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
            var bestiary_path = dirs[0];
            bestiary_path = Path.Join(bestiary_path, "data", "bestiary");
            Debug.WriteLine("Besteiary path is " + bestiary_path);
            var bestiary_files = Directory.GetFiles(bestiary_path);

            List<StopGapMonsterClass> monsters = new List<StopGapMonsterClass>();
            //ignore everything but the bestiary files (eg the fluff files) for now

            foreach (var bestiary_file in bestiary_files)
            {
                var fname = Path.GetFileName(bestiary_file);
                if (!fname.StartsWith("bestiary"))
                {
                    Debug.WriteLine("Skipping file: " + bestiary_file);
                    continue;
                }
                Debug.WriteLine("Processing file: " + bestiary_file);
                //read file
                //TODO: Use streamreader if files are super big but surely this doesn't happen
                Monster_root mon_root = JsonConvert.DeserializeObject<Monster_root>(File.ReadAllText(bestiary_file));
                monsters.AddRange(mon_root.Monsters);

            }
            //remove null elements from list
            //because uhhhhhhhh
            //returning a null reference was the best way I had to deserialise incorrect json 
            //unless im stupid which I probably am and theres a much better way
            monsters.RemoveAll(x => x == null);
            return monsters;

        }

        public static async Task Insert_Data_marten(IDocumentStore store, List<StopGapMonsterClass> monsters)
        {
            //bulkinsertasync uses copy to insert data all in one transaction, very handy for something like this
            //overwrite existing data if it exists so we can update documents without clearing out the whole db
            await store.BulkInsertAsync(monsters, BulkInsertMode.OverwriteExisting);


            
        }
    }


}
