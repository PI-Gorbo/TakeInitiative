namespace TakeInitiative.Api.Features.Combats;

public record Condition(Guid Id, string Name, int StartingTurnNumber, int StaringRoundNumber, string Note);

