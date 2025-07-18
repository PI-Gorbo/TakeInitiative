
using System.Diagnostics;
using System.IO.Compression;
using System.Threading;

using LiteDB;

using Microsoft.Playwright;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using TakeInitiative.Bestiary.Domain.JSON;
namespace TakeInitiative.Bestiary.IngestionScript;

public static class Program
{

    static readonly HttpClient client = new HttpClient();
    const string downloadUrl = "https://api.github.com/repos/5etools-mirror-3/5etools-src/releases/latest";
    const string bestiaryBaseUrl = "https://5e.tools/data/bestiary/";
    const string bestiaryIndexUrl = "https://5e.tools/data/bestiary/index.json";
    public static async Task Main(string[] args)
    {

        var litedb = new LiteDatabase("BestiaryDB.db");
        var monsters = new List<StopGapMonsterClass>();

        try
        {
            monsters = await DownloadDataSrc5etools();
        }
        catch (Exception e)
        {
            Debug.WriteLine("Error downloading data from 5etools: " + e.Message);
            Debug.WriteLine("Falling back to downloading from src github");
            monsters = await DownloadDataSrcGithub();
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

    public static async Task<List<StopGapMonsterClass>> DownloadDataSrcGithub()
    {

        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/111.0.0.0 Safari/537.36");

        string responseBody = await client.GetStringAsync(downloadUrl);

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


        //linq method
        var monsters = bestiary_files
            //filter out irrelevant files
            .Where(file => {
                var fname = Path.GetFileName(file);
                return fname.StartsWith("bestiary");
            })
            //then run for each valid file
            .Select(bestiary_file => {
                Debug.WriteLine("Processing file: " + bestiary_file);
                MonsterRoot mon_root = JsonConvert.DeserializeObject<MonsterRoot>(File.ReadAllText(bestiary_file));
                return mon_root.Monsters;
            })
            //selectmany here flattens the list of lists into a single list
            .SelectMany(x => x)
            .ToList();




        return monsters;

    }

    public static async Task<List<StopGapMonsterClass>> DownloadDataSrc5etools()
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

        var response = await page.GotoAsync(bestiaryIndexUrl);
        string re = await response.TextAsync();
        //Debug.WriteLine(re);
        JObject jobject = JObject.Parse(re);


        //linq method
        var urls_to_download = jobject.PropertyValues().Select(jtoken =>
        {
            var bestiary_file = jtoken.ToString();

            var file_url = bestiaryBaseUrl + bestiary_file;
            return file_url;
        }).ToList();

        Task<List<StopGapMonsterClass>>[] tasks = new Task<List<StopGapMonsterClass>>[urls_to_download.Count];
        //var monsters = new List<StopGapMonsterClass>();
        for (int i = 0; i < urls_to_download.Count; i++)
        {

            var task_page = await browser.NewPageAsync(new BrowserNewPageOptions
            {
                UserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/111.0.0.0 Safari/537.36"
            });

            string url = urls_to_download[i];
            //Debug.WriteLine("Downloading file: " + url);
            tasks[i] = Task.Run(() => DownloadMonstersFromUrl(url, task_page));
        }

        List<StopGapMonsterClass>[] pages = await Task.WhenAll(tasks);
        List<StopGapMonsterClass> monsters = pages.SelectMany(x => x).ToList();
        return monsters;

    }
    private static async Task<List<StopGapMonsterClass>> DownloadMonstersFromUrl(string url, IPage page)
    {
        var response = await page.GotoAsync(url);
        string re = await response.TextAsync();

        Debug.WriteLine("deserializing JSON from URL: " + url);
        MonsterRoot r;
        try
        {
            r = JsonConvert.DeserializeObject<MonsterRoot>(re);
        }
        catch (JsonReaderException e){
            Debug.WriteLine("Error deserializing JSON from URL: " + url);
            throw;
        }
        

        //Use lock to prevent race conditions
        //lock (_lock)
        //{
        //    monsters.AddRange(r.Monsters);
        //}
        return r.Monsters;
    }

    public static async Task Insert_Data_LiteDB(LiteDatabase litedb, List<StopGapMonsterClass> monsters)
    {
        var mapper = BsonMapper.Global;
        mapper.SerializeNullValues = true; // Serialize null values to ensure all fields are stored            
        mapper.IncludeFields = true; // Include all fields in serialization
        mapper.IncludeNonPublic = true; // Include non-public fields in serialization

        mapper.Entity<StopGapMonsterClass>()
            .Id(x => x._5etools_link)
            .Field(x => x.Dex, "Dex");
        
        


        var collection = litedb.GetCollection<StopGapMonsterClass>("Bestiary_monsters");
        //foreach (StopGapMonsterClass monster in monsters)
        //{
        //    //var doc = BsonMapper.Global.ToDocument(monster); // Ensure the monster is serialized correctly before insertion
        //    //Debug.WriteLine(doc["Dex"].AsInt32);

        //    collection.Upsert(monster);
        //}
        //insert noncopy monsters
        monsters
            .ForEach(monster =>
            {
                if (monster.Copy == null)
                {
                    collection.Upsert(monster);
                }

            });



        var normalisedMonsters = monsters
            .Where(monster => monster.Copy != null)
            .Select(monster => {
                var m =NormaliseMonsters.NormaliseMonster(litedb, monster);
                collection.Upsert(m);
                return m;
            })
            .ToList();

        collection.EnsureIndex(x => x.Name);
        //TODO: ACTUALLY INSERT INTO DB
        MonsterRoot normRoot = new MonsterRoot
        {
            Monsters = normalisedMonsters
        };
        File.WriteAllText("DebugMonsterNormalisation.json", JsonConvert.SerializeObject(normalisedMonsters, formatting: Formatting.Indented));

        

        //debug output
        MonsterRoot root = new MonsterRoot
        {
            Monsters = monsters
        };
        File.WriteAllText("DebugMonsterSerialization.json", JsonConvert.SerializeObject(root, formatting:Formatting.Indented));
    }
}
