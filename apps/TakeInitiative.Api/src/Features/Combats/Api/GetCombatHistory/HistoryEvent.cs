namespace TakeInitiative.Api.Features.Combats;

public record HistoryEvent
{
    public required string EventName { get; set; }
    public required Guid UserId { get; set; }
}
