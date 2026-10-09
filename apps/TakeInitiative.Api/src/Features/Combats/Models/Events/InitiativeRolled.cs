namespace TakeInitiative.Api.Features.Combats;

/// <summary>
/// A roll (18b). The first one starts a <c>Draft</c>: it becomes <c>Active</c> in round 1, in
/// <see cref="SessionId"/> (the current session when rolled), with the turn on the top of the
/// order. Later rolls slot the late joiners in without moving the turn.
/// </summary>
public sealed record InitiativeRolled(Actor Actor, Guid? SessionId, InitiativeRollResult[] Rolls) : IActorEvent;

/// <summary>One combatant's roll: the total that becomes its initiative, and how it was rolled.</summary>
public sealed record InitiativeRollResult(Guid CombatantId, int Total, string Roll, string Evaluation);
