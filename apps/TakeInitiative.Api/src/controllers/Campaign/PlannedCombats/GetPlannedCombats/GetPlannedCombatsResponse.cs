using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Controllers;

public class GetPlannedCombatsResponse
{
    public required PlannedCombat[] PlannedCombats { get; set; }
}
