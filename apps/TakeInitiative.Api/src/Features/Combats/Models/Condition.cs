namespace TakeInitiative.Api.Features.Combats;

public record Condition
{
    public required string Name { get; set; }
    public required ConditionDuration Duration { get; set; }
    public required string? Note { get; set; }
}

