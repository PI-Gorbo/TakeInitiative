namespace TakeInitiative.Api.Features.Sessions;

/// <summary>A DM unhid the note.</summary>
public sealed record SessionNoteUnhidden(Actor Actor) : IActorEvent;
