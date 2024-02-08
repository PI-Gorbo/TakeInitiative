using CSharpFunctionalExtensions;
using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Controllers;
public record PutPlannedCombatRequest
{
    public required Guid CombatId { get; set; }
    public required Guid CampaignId { get; set; }
    public required string? CombatName { get; set; }
    public required List<PlannedCombatStage>? Stages { get; set; }
}