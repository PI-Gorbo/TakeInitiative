using FastEndpoints;
using Marten;
using JasperFx;
using Marten.Services.Json;

namespace TakeInitiative.BestiaryAPI.GetMonster;

public class Search_Request
{
    public string Name { get; set; } 
}
