using Microsoft.AspNetCore.Mvc;

namespace TakeInitiative.Api.Features.Combats;


public record GetCombatRequest
{
    [FromRoute]
    public Guid Id { get; set; }
}
