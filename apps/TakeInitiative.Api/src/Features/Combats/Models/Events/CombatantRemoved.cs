namespace TakeInitiative.Api.Features.Combats;

/// <summary>Removes a combatant. When it had the turn, the turn passes to the next in the order.</summary>
public sealed record CombatantRemoved(Actor Actor, Guid CombatantId) : IActorEvent;
