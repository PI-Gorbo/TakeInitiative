namespace TakeInitiative.Api.Features.Combats;

public class GetCombatsResponse
{
    public required PlannedCombat[] PlannedCombats { get; set; }
    public required CombatDto[] Combats { get; set; }
}
