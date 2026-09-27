namespace TakeInitiative.Api.Features.Entries;

/// <summary>The entry's name changed. Mentions are stored by id (invariant 6), so no text is rewritten.</summary>
public sealed record EntryRenamed(Actor Actor, string Name) : IActorEvent;
