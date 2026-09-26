namespace TakeInitiative.Api.Features.Entries;

/// <summary>One alias was removed.</summary>
public sealed record EntryAliasRemoved(Actor Actor, string Alias) : IActorEvent;
