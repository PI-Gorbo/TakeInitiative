namespace TakeInitiative.Api.Features.Sessions;

/// <summary>Starts a Session stream (stream id = session id). Any member starts the next session.</summary>
public sealed record SessionStarted(Actor Actor, Guid CampaignId, int Number) : IActorEvent;
