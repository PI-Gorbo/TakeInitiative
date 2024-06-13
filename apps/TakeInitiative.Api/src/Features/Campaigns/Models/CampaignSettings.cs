using Xunit.Sdk;

namespace TakeInitiative.Api.Features.Campaigns;
public record CombatHealthDisplaySettings
{
    public enum DisplayOptions
    {
        RealValue = 0,
        HealthyBloodied = 1,
        Hidden = 2
    }

    public DisplayOptions DmCharacterDisplayMethod { get; set; } = DisplayOptions.HealthyBloodied;
    public DisplayOptions OtherPlayerCharacterDisplayMethod { get; set; } = DisplayOptions.RealValue;
}

public record CombatArmourClassDisplaySettings
{
    public enum DisplayOptions
    {
        RealValue = 0,
        Hidden = 2
    }

    public DisplayOptions DmCharacterDisplayMethod { get; set; } = DisplayOptions.Hidden;
    public DisplayOptions OtherPlayerCharacterDisplayMethod { get; set; } = DisplayOptions.RealValue;
}
public record CampaignSettings
{
    public CombatHealthDisplaySettings CombatHealthDisplaySettings { get; set; } = new();
    public CombatArmourClassDisplaySettings CombatArmourClassDisplaySettings { get; set; } = new();
}