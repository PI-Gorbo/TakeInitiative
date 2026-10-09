namespace TakeInitiative.Api.Features.Sessions;

/// <summary>The author deleted the note. The projection deletes the document; the stream keeps every event.</summary>
public sealed record SessionNoteDeleted(Actor Actor) : IActorEvent;
