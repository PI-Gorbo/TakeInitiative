namespace TakeInitiative.Api.Features.Combats;

/// <summary>Starts a Combat stream (stream id = combat id) as a <c>Draft</c> in the current session. DMs only.</summary>
public sealed record CombatCreated(Actor Actor, Guid CampaignId, Guid SessionId, string Name) : IActorEvent;
