using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Controllers;

public record CombatCharacterDto : Character
{
    public required bool IsHidden {get; set;}
    public required int[] InitiativeValue {get; set;}
}
