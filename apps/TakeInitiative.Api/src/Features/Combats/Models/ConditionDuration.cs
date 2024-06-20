namespace TakeInitiative.Api.Features.Combats;

public record ConditionDuration
{
    public enum ConditionDurationType
    {
        Rounds_StartOfRound = 0, // When the value is 0, remove the condition. When the value is 1, the condition will continue to the end of round.
        Rounds_OnCharacterTurn = 1, // When the value is 0, remove the condition. When the value is 1, the condition will continue to the end of round.
    }

    public required ConditionDurationType Type { get; set; }
    public required uint Value { get; set; }
}

