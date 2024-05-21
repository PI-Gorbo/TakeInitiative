namespace TakeInitiative.Api.Features.Campaigns;

public record CombatHistoryDto
{
    public required DateTimeOffset? LastCombatTimestamp { get; set; }
    public required int TotalCombats { get; set; }
}