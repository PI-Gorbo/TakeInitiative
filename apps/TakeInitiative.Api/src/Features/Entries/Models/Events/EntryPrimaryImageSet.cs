namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// The entry's primary image (SAM-12): the one picture that stands for it on its page, in the
/// wiki list, in search hits and on a combat row. Only an image everyone can see may be one,
/// so the id is safe for the entry's whole audience and rides <c>entryUpserted</c>.
/// </summary>
public sealed record EntryPrimaryImageSet(Actor Actor, Guid ImageId) : IActorEvent;
