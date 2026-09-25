namespace TakeInitiative.Api.Features.Entries;

/// <summary>The entry is no longer anyone's player character.</summary>
public sealed record EntryUnclaimed(Actor Actor) : IActorEvent;
