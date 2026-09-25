namespace TakeInitiative.Api.Features.Sessions;

/// <summary>
/// The author edited the note. Every version stays in the stream (edit history).
/// <see cref="Images"/> (step 16b) is the whole ordered list after the edit, or null when the
/// images did not change (and in every event from before step 16).
/// </summary>
public sealed record SessionNoteEdited(Actor Actor, string Text, bool IsRecap, NoteImage[]? Images = null) : IActorEvent;
