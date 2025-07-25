using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using FluentAssertions;
using Newtonsoft.Json;
using TakeInitiative.Bestiary.IngestionScript;
using TakeInitiative.Bestiary.Domain.JSON;
using LiteDB;
using System.Diagnostics;
namespace TakeInitiative.Bestiary.Tests.Unit;

//TODO: Figure out how class fixtures work and 
//setup the testing db with that
//would be a problem if we had more than one test lol
public  class TestBestiaryNormalisation
{
    [Fact]
    public async Task TestNormalisation()
    {
        DirectoryInfo di = new DirectoryInfo(Directory.GetCurrentDirectory());
        Directory.SetCurrentDirectory(di.Parent.Parent.Parent.FullName);


        string filename = "./Resources/bestiary-cos.json";
        MonsterRoot root = JsonConvert.DeserializeObject<MonsterRoot>(File.ReadAllText(filename));


        string mmFilename = "./Resources/bestiary-mm.json";
        var mmRoot = JsonConvert.DeserializeObject<MonsterRoot>(File.ReadAllText(mmFilename));

        //make new litedb in memory
        using var ms = new MemoryStream();
        using (var db = new LiteDatabase(ms))
        {
            var collection = db.GetCollection<StopGapMonsterClass>("Bestiary_monsters");
            mmRoot.Monsters.ForEach(monster =>
            {
                Debug.WriteLine("Inserting MOnster: {0}", monster.Name);
                collection.Insert(monster);
            });

            //now find creature and normalise
            var d = root.Monsters.First(x => x.Name.Equals("Doru"));

            var doru = NormaliseMonsters.NormaliseMonster(db, d);

            doru.Should().NotBeNull();
            doru.Ac.Should().NotBeNullOrEmpty();

            AcObject acobject = new AcObject()
            {
                Ac = 15,
                From = new List<string> { "natural armor" }

            };
            doru.Ac.Should().ContainEquivalentOf<AcObject>(acobject);
            doru.Dex.Should().Be(16);
            Hp hp = new Hp()
            {
                Average = 82,
                Formula = "11d8 + 33"
            };
            doru.Hp.Should().BeEquivalentTo(hp);
            doru.Initiative.Initiative.Should().Be(3);
        }





    }
}
