namespace TakeInitiative.Api.Features.Sessions;

/// <summary>A DM set or cleared (<c>null</c>) the session's title.</summary>
public sealed record SessionTitleChanged(Actor Actor, string? Title) : IActorEvent;
