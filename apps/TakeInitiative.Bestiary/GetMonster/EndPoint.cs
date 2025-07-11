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
namespace TakeInitiative.BestiaryAPI.GetMonster;


public class EndPoint(LiteDBContext litedbcontext) : Endpoint<Search_Request, Search_Response>
{
    public override void Configure()
    {
        Get("/search");
        AllowAnonymous();
    }
    public override async Task HandleAsync(Search_Request request, CancellationToken ct)
    {
        string request_name = request.Name;
        
        Debug.WriteLine("Searching for monsters with name containing: " + request_name);

        using var litedb = litedbcontext.GetSession();
        var collection = litedb.GetCollection<StopGapMonsterClass>("Bestiary_monsters");
        var monsters = collection.Query()
            .Where(x => x.Name.Contains(request_name))
            .ToList();


        Debug.WriteLine("found {0} monsters", monsters.Count());



        MonsterRoot mr = new MonsterRoot();
        mr.Monsters = monsters.ToList();
        Search_Response response = new Search_Response();
        response.monsters = mr;
        await SendAsync(response);
        

    }
}
