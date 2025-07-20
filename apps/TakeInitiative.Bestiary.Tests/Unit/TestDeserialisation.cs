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
namespace TakeInitiative.Bestiary.Tests.Unit;

public class TestBestiary
{
    
    
    [Fact]
    public async Task TestDeserialisation()
    {
        //this is cancer why must I do this to make the CWD the root directory of the project instead of the stupid /bin/debug folder
        DirectoryInfo di = new DirectoryInfo(Directory.GetCurrentDirectory());
        Directory.SetCurrentDirectory(di.Parent.Parent.Parent.FullName);



        string filename = "./Resources/bestiary-cos.json";
        MonsterRoot root = JsonConvert.DeserializeObject<MonsterRoot>(File.ReadAllText(filename));

        //var doru = root.Monsters.First(x => x.Name.Equals("Doru"));

        //doru.Should().NotBeNull();
        //doru.Ac.Should().NotBeNullOrEmpty();
        //doru.Ac.Should().ContainSingle(x => x.Ac == 15 && x.Type == "natural");

        var strahdVonZarovich = root.Monsters.First(x => x.Name.Equals("Strahd von Zarovich"));

        strahdVonZarovich.Should().NotBeNull();
        strahdVonZarovich.Ac.Should().NotBeNullOrEmpty();

        AcObject acobject = new AcObject()
        {
            Ac = 16,
            From = new List<string> { "natural armor" }

        };

        


        strahdVonZarovich.Ac.Should().ContainSingle(x => x.Ac == 16);
        strahdVonZarovich.Ac.Should().ContainEquivalentOf<AcObject>(acobject);

        Hp hp = new Hp()
        {
            Average = 144,
            Formula = "17d8 + 68"
        };

        strahdVonZarovich.Hp.Should().BeEquivalentTo(hp);

        strahdVonZarovich.Initiative.Initiative.Should().Be(4);
        strahdVonZarovich.Dex.Should().Be(18);
        strahdVonZarovich.Cr.Cr.Should().Be(15);

    }
}
