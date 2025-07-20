using FastEndpoints;
using Marten;
using JasperFx;
using Marten.Services.Json;

namespace TakeInitiative.BestiaryAPI.GetMonster;

public class SearchRequest
{
    public string Name { get; set; } 
}
