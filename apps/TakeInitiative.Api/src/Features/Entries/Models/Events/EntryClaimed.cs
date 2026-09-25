namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// A member claimed this Character entry as their player character (glossary: Claim), or a DM
/// assigned it to them. <see cref="MemberId"/> is the claimer, not necessarily the actor.
/// </summary>
public sealed record EntryClaimed(Actor Actor, Guid MemberId) : IActorEvent;
