namespace TakeInitiative.Api.Features.Combats;

/// <summary>
/// One combatant's whole editable state after an edit (<see cref="CombatantState"/>), so
/// replay needs no merge rules. Edits are last-write-wins per combatant.
/// </summary>
public sealed record CombatantEdited(Actor Actor, Guid CombatantId, CombatantState State) : IActorEvent;
