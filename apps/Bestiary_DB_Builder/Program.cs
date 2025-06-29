
using System.Diagnostics;
using System.IO.Compression;
using System.Threading;

using LiteDB;

using Microsoft.Playwright;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using TakeInitiative.Bestiary.Domain.JSON;
namespace TakeInitiative.Bestiary.IngestionScript
{
    public static class Program
    {

        private static readonly Lock _lock = new();

        static readonly HttpClient client = new HttpClient();
        const string download_url = "https://api.github.com/repos/5etools-mirror-3/5etools-src/releases/latest";
        public static async Task Main(string[] args)
        {

            var litedb = new LiteDatabase("BestiaryDB.db");
            var monsters = new List<StopGapMonsterClass>();

            try
            {
                monsters = await Download_Data_Src5etools();
            }
            catch (Exception e)
            {
                Debug.WriteLine("Error downloading data from 5etools: " + e.Message);
                Debug.WriteLine("Falling back to downloading from src github");
                monsters = await Download_Data_SrcGithub();
                //Clean up 5etools folder, delete it
                //sometimes there is an error it usually doesn't matter though
                try
                {
                    if (Directory.Exists("5etools"))
                    {
                        Directory.Delete("5etools", true);
                    }
                }
                catch (Exception e_0)
                {
                    Debug.WriteLine("Error deleting 5etools in cleanup: " + e_0.Message);
                }
            }

            //remove null elements from list
            //because uhhhhhhhh
            //returning a null reference was the best way I had to deserialise incorrect json 
            //unless im stupid which I probably am and theres a much better way
            monsters.RemoveAll(x => x == null);

            Debug.WriteLine("Downloaded {0} monsters from 5etools", monsters.Count);
            //clean up
            Debug.WriteLine("Done inserting {0} monsters into DB", monsters.Count);


            //insert data into litedb
            await Insert_Data_LiteDB(litedb, monsters);

            //IMPORTANT: if we dont dispose the db, the write log will stay open and keep accumulating in size!
            litedb.Dispose();
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

            return monsters;

        }

        public static async Task<List<StopGapMonsterClass>> Download_Data_Src5etools()
        {
            using var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,

            });
            var page = await browser.NewPageAsync(new BrowserNewPageOptions
            {
                UserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/111.0.0.0 Safari/537.36"
            });

            var response = await page.GotoAsync("https://5e.tools/data/bestiary/index.json");
            string re = await response.TextAsync();
            //Debug.WriteLine(re);
            JObject jobject = JObject.Parse(re);

            var urls_to_download = new List<string>();

            foreach (JToken jtoken in jobject.PropertyValues())
            {
                var bestiary_file = jtoken.ToString();
                const string base_url = "https://5e.tools/data/bestiary/";
                var file_url = base_url + bestiary_file;
                urls_to_download.Add(file_url);

            }
            Task[] tasks = new Task[urls_to_download.Count];
            var monsters = new List<StopGapMonsterClass>();
            for (int i = 0; i < urls_to_download.Count; i++)
            {

                var task_page = await browser.NewPageAsync(new BrowserNewPageOptions
                {
                    UserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/111.0.0.0 Safari/537.36"
                });

                string url = urls_to_download[i];
                //Debug.WriteLine("Downloading file: " + url);
                tasks[i] = Task.Run(() => down_and_des(url, task_page, monsters));
            }

            await Task.WhenAll(tasks);
            return monsters;

        }
        private static async Task down_and_des(string url, IPage page, List<StopGapMonsterClass> monsters)
        {
            var response = await page.GotoAsync(url);
            string re = await response.TextAsync();
            Monster_root r = JsonConvert.DeserializeObject<Monster_root>(re);
            //Debug.WriteLine("waiting mutex");

            //Debug.WriteLine("Adding " + r.Monsters.Count + " to mons");
            //Use lock to prevent race conditions
            lock (_lock)
            {
                monsters.AddRange(r.Monsters);
            }
            //mutex.WaitOne();

            //mutex.ReleaseMutex();
        }

        public static async Task Insert_Data_LiteDB(LiteDatabase litedb, List<StopGapMonsterClass> monsters)
        {

            BsonMapper.Global.SerializeNullValues = true; // Serialize null values to ensure all fields are stored

            var mapper = BsonMapper.Global;
            mapper.Entity<StopGapMonsterClass>()
                .Id(x => x._5etools_link);


            var collection = litedb.GetCollection<StopGapMonsterClass>("Bestiary_monsters");
            foreach (StopGapMonsterClass monster in monsters)
            {
                collection.Upsert(monster);
            }
            collection.EnsureIndex(x => x.Name);
        }
    }
}
