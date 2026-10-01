namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// A link was removed from the entry (27b). Only its id: what it pointed at is on the
/// <see cref="EntryLinkAdded"/> earlier in the stream, which is where history reads it from.
/// </summary>
public sealed record EntryLinkRemoved(Actor Actor, Guid LinkId) : IActorEvent;
