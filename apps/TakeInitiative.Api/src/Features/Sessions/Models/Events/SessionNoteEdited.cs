namespace TakeInitiative.Api.Features.Sessions;

/// <summary>
/// The author edited the note. Every version stays in the stream (edit history).
/// <see cref="Images"/> (step 16b) is the whole ordered list after the edit, or null when the
/// images did not change (and in every event from before step 16).
/// <para>
/// When the edit accepted a suggestion (step 23c), the <see cref="Actor"/> is still the author and
/// carries the model (<see cref="Actor.Model"/>), and <see cref="Suggestion"/> says which span it
/// linked. Both are null on every other edit, a revert included (the author's own act).
/// </para>
/// </summary>
public sealed record SessionNoteEdited(
    Actor Actor, string Text, bool IsRecap, NoteImage[]? Images = null, SuggestedSpan? Suggestion = null) : IActorEvent;
