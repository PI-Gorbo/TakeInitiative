namespace TakeInitiative.Api.Features.Combats;

// Here, the term 'Operator' always refers to the actual person that performed the event. 
// If there is no 'Operator', then the action was performed by the system.
public record CombatLog(ICombatOperation[] Operations, Guid OperatorId, DateTimeOffset Time);
