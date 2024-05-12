using System.ComponentModel.DataAnnotations;

namespace TakeInitiative.Api.Features;

public record PutPlannedCombatStageRequest
{
    public Guid CombatId { get; set; }
    public Guid StageId { get; set; }
    public string Name { get; set; }
}
