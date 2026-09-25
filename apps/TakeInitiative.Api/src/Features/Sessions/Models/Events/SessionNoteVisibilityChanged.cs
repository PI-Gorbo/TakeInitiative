namespace TakeInitiative.Api.Features.Sessions;

/// <summary>The author changed who can see the note.</summary>
public sealed record SessionNoteVisibilityChanged(Actor Actor, Visibility Visibility) : IActorEvent;
