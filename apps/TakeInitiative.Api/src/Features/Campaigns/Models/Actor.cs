namespace TakeInitiative.Api.Features.Campaigns;

/// <summary>
/// Who caused an event (glossary: Actor). Today it is always a member of the
/// campaign. It is a record rather than a bare id so a model case
/// (<c>Model { Name, Version, Confidence }</c>, design §11a) can be added later
/// without changing every event.
/// </summary>
public sealed record Actor(Guid MemberId)
{
    public static Actor Member(Guid memberId) => new(memberId);
}

/// <summary>Every event carries the Actor that caused it (invariant 9).</summary>
public interface IActorEvent
{
    Actor Actor { get; }
}
