using Xunit.Sdk;

namespace TakeInitiative.Api.Features.Campaigns;
public record CombatHealthDisplaySettings
{
    public enum CombatHealthDisplayOptions
    {
        RealValue = 0,
        HealthyBloodied = 1,
        Hidden = 2
    }

    public CombatHealthDisplayOptions DmCharacterDisplayMethod { get; set; } = CombatHealthDisplayOptions.HealthyBloodied;
    public CombatHealthDisplayOptions OtherPlayerCharacterDisplayMethod { get; set; } = CombatHealthDisplayOptions.RealValue;
}

public record CombatArmourClassDisplaySettings
{
    public enum CombatArmourDisplayOptions
    {
        RealValue = 0,
        Hidden = 2
    }

    public CombatArmourDisplayOptions DmCharacterDisplayMethod { get; set; } = CombatArmourDisplayOptions.Hidden;
    public CombatArmourDisplayOptions OtherPlayerCharacterDisplayMethod { get; set; } = CombatArmourDisplayOptions.RealValue;
}
public record CampaignSettings
{
    public CombatHealthDisplaySettings CombatHealthDisplaySettings { get; set; } = new();
    public CombatArmourClassDisplaySettings CombatArmourClassDisplaySettings { get; set; } = new();
}