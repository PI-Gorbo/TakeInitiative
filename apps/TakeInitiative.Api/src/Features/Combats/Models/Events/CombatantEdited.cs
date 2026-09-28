namespace TakeInitiative.Api.Features.Combats;

/// <summary>
/// One combatant's whole editable state after an edit (<see cref="CombatantState"/>), so
/// replay needs no merge rules. Edits are last-write-wins per combatant.
/// <para>
/// <see cref="Tiebreak"/> is set only by a DM's reorder (18b.4), which is the one write that
/// moves a combatant between equal initiatives. Null keeps the combatant's own.
/// </para>
/// </summary>
public sealed record CombatantEdited(Actor Actor, Guid CombatantId, CombatantState State, int? Tiebreak = null) : IActorEvent;
