namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// The entry has no primary image again (SAM-12). Appended by the member who removed it, and by
/// <see cref="EntryPrimaryImages"/> when the image stops being one everyone can see.
/// </summary>
public sealed record EntryPrimaryImageCleared(Actor Actor) : IActorEvent;
