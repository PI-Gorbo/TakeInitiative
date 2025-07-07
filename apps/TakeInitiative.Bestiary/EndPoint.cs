using System.Diagnostics;
using System.Threading;

using FastEndpoints;

using JasperFx;

using LiteDB;

using Marten;
using Marten.Services.Json;

using Newtonsoft;

using TakeInitiative.Bestiary.Domain.JSON;
using TakeInitiative.BestiaryAPI.Startup;
namespace TakeInitiative.BestiaryAPI;

//Here we use iquerysession as this is a read only session
public class EndPoint(LiteDBContext litedbcontext) : Endpoint<Search_Request, Search_Response>
{
    public override void Configure()
    {
        Post("/search");
        AllowAnonymous();
    }
    public override async Task HandleAsync(Search_Request request, CancellationToken ct)
    {
        string request_name = request.Name;
        //query session, returns readonly list
        Debug.WriteLine("Searching for monsters with name containing: " + request_name);

        using var litedb = litedbcontext.GetSession();
        var collection = litedb.GetCollection<StopGapMonsterClass>("Bestiary_monsters");
        var monsters = collection.Query()
            .Where(x => x.Name.Contains(request_name))
            .ToList();


        Debug.WriteLine("found {0} monsters", monsters.Count());


        foreach (var m in monsters)
        {
            Debug.WriteLine("Found monster: {0} with dex: {1}", m.Name, m.Dex);
        }


        MonsterRoot mr = new MonsterRoot();
        mr.Monsters = monsters.ToList<StopGapMonsterClass>();
        Search_Response response = new Search_Response();
        response.monsters = mr;
        await SendAsync(response);

    }
}
