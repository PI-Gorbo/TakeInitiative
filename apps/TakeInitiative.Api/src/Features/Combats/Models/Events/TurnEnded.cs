namespace TakeInitiative.Api.Features.Combats;

/// <summary>The turn moves from one combatant to the next, in <see cref="Round"/> (18b).</summary>
public sealed record TurnEnded(Actor Actor, Guid FromCombatantId, Guid? ToCombatantId, int Round) : IActorEvent;
