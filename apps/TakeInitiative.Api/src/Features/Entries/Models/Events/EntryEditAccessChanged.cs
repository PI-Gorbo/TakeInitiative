namespace TakeInitiative.Api.Features.Entries;

/// <summary>The creator or a DM changed who can edit the entry.</summary>
public sealed record EntryEditAccessChanged(Actor Actor, EditAccess EditAccess) : IActorEvent;
