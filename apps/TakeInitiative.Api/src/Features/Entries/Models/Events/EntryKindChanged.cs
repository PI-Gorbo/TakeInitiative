namespace TakeInitiative.Api.Features.Entries;

/// <summary>The entry's kind changed.</summary>
public sealed record EntryKindChanged(Actor Actor, EntryKind Kind) : IActorEvent;
