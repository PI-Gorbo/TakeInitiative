namespace TakeInitiative.Api.Models;

public record CharacterHealth
{
	public int? MaxHealth { get; set; } = 0;
    public int CurrentHealth { get; set; } = 0;

    public override string ToString()
    {
		if (MaxHealth == null) {
			return $"{CurrentHealth}";
		}

        if (CurrentHealth != MaxHealth)
        {
            return $"{CurrentHealth} / {MaxHealth}";
        }

        return $"{MaxHealth}";
    }
}