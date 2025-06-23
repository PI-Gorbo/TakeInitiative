using FastEndpoints;
using Marten;
using JasperFx;
using Marten.Services.Json;
using BestiaryAPI.JSON;
using System.Diagnostics;

namespace BestiaryAPI
{
    //Here we use iquerysession as this is a read only session
    public class EndPoint(IQuerySession session) : Endpoint<Search_Request, Search_Response>
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
            var monsters = await session.Query<StopGapMonsterClass>()
                .Where(x => x.Name.Contains(request_name))
                .ToListAsync<StopGapMonsterClass>();
            //var monsters = session.Query<StopGapMonsterClass>()
            //    .Where(x => x.Name.Contains(request_name))
            //    .ToList<StopGapMonsterClass>();
            //List<StopGapMonsterClass> monsters = await session.QueryAsync<StopGapMonsterClass>().Where(x => x.Name.Contains(request_name));

            Debug.WriteLine("found {0} monsters", monsters.Count());

            Monster_root mr = new Monster_root();
            mr.Monsters = monsters.ToList<StopGapMonsterClass>();
            Search_Response response = new Search_Response();
            response.monsters = mr;
            await SendAsync(response);

        }
    }
}
