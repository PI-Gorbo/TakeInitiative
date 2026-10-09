namespace TakeInitiative.Api.Features.Campaigns;

/// <summary>A user joined by join code. They join as a Player.</summary>
public sealed record MemberJoined(Actor Actor, Guid MemberId, Guid UserId) : IActorEvent;
