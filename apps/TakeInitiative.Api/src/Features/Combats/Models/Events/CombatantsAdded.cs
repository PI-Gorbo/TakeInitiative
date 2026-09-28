namespace TakeInitiative.Api.Features.Combats;

/// <summary>
/// Adds waiting combatants, with their HP already rolled and their <c>Tiebreak</c> drawn, so
/// replay rolls nothing.
/// </summary>
public sealed record CombatantsAdded(Actor Actor, Combatant[] Combatants) : IActorEvent;
