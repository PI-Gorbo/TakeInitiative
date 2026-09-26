namespace TakeInitiative.Api.Features.Campaigns;

/// <summary>Starts a Campaign stream. The owner becomes the first member, as a DM.</summary>
public sealed record CampaignCreated(
    Actor Actor,
    string Name,
    Guid OwnerMemberId,
    Guid OwnerUserId,
    string JoinCode) : IActorEvent;
