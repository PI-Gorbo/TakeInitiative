namespace TakeInitiative.Api.Models;

public enum InitiativeStrategy
{
    Fixed = 0,
    Roll = 1
}

public record CharacterInitiative
{
    public required InitiativeStrategy Strategy { get; set; }

    public required string Value { get; set; }

    public int? Fixed
    {
        get
        {
            if (this.Strategy != InitiativeStrategy.Fixed) return null;

            try
            {
                return Convert.ToInt32(this.Value);
            }
            catch
            {
                return 0;
            }
        }
    }

    public string? Roll
    {
        get
        {
            if (this.Strategy != InitiativeStrategy.Roll) return null;
            return this.Value;
        }
    }
}
