namespace TakeInitiative.Api.Features.Campaigns;

public record CombatHistoryDto
{
    public required Guid CombatId { get; set; }
    public required string CombatName { get; set; }
    public required DateTimeOffset FinishedOn { get; set; }
}