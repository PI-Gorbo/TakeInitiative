using FastEndpoints;
using Marten;
using JasperFx;
using Marten.Services.Json;
using BestiaryAPI.JSON;

namespace BestiaryAPI
{
    public class EndPoint : Endpoint<Search_Request, Search_Response>
    {
        public override void Configure()
        {
            Post("/search");
            AllowAnonymous();
        }
        public override async Task HandleAsync(Search_Request request, CancellationToken ct)
        {
            monster_root mr = new monster_root();
            Search_Response response = new Search_Response();
            response.monsters = mr;
            await SendAsync(response);
        }
    }
}
