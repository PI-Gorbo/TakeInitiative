using TakeInitiative.Api.Features;

namespace TakeInitiative.Api.Features.Combats;

public class GetPlannedCombatsResponse
{
    public required PlannedCombat[] PlannedCombats { get; set; }
}
