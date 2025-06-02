namespace TakeInitiative.Api.Features.Combats;

public class GetCombatsResponse
{
    public required PlannedCombatDto[] PlannedCombats { get; set; }
    public required CombatDto[] Combats { get; set; }
}
