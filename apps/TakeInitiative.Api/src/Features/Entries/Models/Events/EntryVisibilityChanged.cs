namespace TakeInitiative.Api.Features.Entries;

/// <summary>The creator or a DM changed who can see the entry.</summary>
public sealed record EntryVisibilityChanged(Actor Actor, Visibility Visibility) : IActorEvent;
