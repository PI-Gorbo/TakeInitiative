namespace TakeInitiative.Api.Features.Entries;

/// <summary>One alias was added. The aliases endpoint appends one event per alias, so history reads per alias.</summary>
public sealed record EntryAliasAdded(Actor Actor, string Alias) : IActorEvent;
