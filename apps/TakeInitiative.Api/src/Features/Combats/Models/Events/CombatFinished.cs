namespace TakeInitiative.Api.Features.Combats;

/// <summary>A DM finishes the combat (18b): it becomes read-only, with no turn.</summary>
public sealed record CombatFinished(Actor Actor) : IActorEvent;
