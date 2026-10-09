namespace TakeInitiative.Api.Features.Entries;

/// <summary>The entry's stats (glossary: Stats) after the change. Null clears them.</summary>
public sealed record EntryStatsChanged(Actor Actor, Stats? Stats) : IActorEvent;
