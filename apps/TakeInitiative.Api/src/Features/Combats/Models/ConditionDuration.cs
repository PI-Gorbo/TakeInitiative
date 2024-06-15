namespace TakeInitiative.Api.Features.Combats;

public record ConditionDuration
{
    public enum ConditionDurationType
    {
        Rounds = 0, // When the value is 0, remove the condition. When the value is 1, the condition will continue to the end of round.
        Minutes = 1, // Stored in seconds.
        Hours = 2, // Stored in seconds.
    }

    public ConditionDurationType Type { get; set; }
    public uint Value { get; set; }

    public static ConditionDuration Rounds(uint Rounds) => new() { Type = ConditionDurationType.Rounds, Value = Rounds };

}

