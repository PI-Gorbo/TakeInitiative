using System.Collections.Immutable;

namespace TakeInitiative.Api.Features.Combats;

public record HistoryEntry
{
    public required DateTimeOffset Timestamp { get; set; }
    public required ImmutableList<HistoryEvent> Events { get; set; }
    public required Guid Executor { get; set; }
}
