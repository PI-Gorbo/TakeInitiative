using System.Text.Json.Serialization;

namespace TakeInitiative.Api.Features.Campaigns;

/// <summary>A member's role in a campaign. There can be several DMs; the owner promotes and demotes.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Role>))]
public enum Role
{
    DM,
    Player,
}
