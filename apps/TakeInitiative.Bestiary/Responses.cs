using FastEndpoints;
using Marten;
using JasperFx;
using Marten.Services.Json;
using BestiaryAPI.JSON;

namespace BestiaryAPI

{
    public class Search_Response
    {
        public monster_root monsters { get; set; }
    }
}
