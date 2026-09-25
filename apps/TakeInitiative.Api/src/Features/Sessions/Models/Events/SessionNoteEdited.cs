namespace TakeInitiative.Api.Features.Sessions;

/// <summary>The author edited the note. Every version stays in the stream (edit history).</summary>
public sealed record SessionNoteEdited(Actor Actor, string Text, bool IsRecap) : IActorEvent;
