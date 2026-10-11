namespace TakeInitiative.Api.Features.Campaigns;

/// <summary>A DM renamed the campaign.</summary>
public sealed record CampaignRenamed(Actor Actor, string Name) : IActorEvent;
