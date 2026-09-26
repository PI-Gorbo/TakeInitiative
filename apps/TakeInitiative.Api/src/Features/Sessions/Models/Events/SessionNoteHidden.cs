namespace TakeInitiative.Api.Features.Sessions;

/// <summary>A DM hid the note from everyone except its author and the DMs. It is not a delete.</summary>
public sealed record SessionNoteHidden(Actor Actor) : IActorEvent;
