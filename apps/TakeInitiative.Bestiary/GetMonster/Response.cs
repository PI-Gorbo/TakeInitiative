using FastEndpoints;
using Marten;
using JasperFx;
using Marten.Services.Json;
using TakeInitiative.Bestiary.Domain.JSON;

namespace TakeInitiative.BestiaryAPI.GetMonster;


public class SearchResponse
{
    public MonsterRoot monsters { get; set; }
}
