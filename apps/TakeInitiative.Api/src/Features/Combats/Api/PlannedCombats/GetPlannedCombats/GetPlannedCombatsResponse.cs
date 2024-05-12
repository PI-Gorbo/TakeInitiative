using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Features;

public class GetPlannedCombatsResponse
{
    public required PlannedCombat[] PlannedCombats { get; set; }
}
