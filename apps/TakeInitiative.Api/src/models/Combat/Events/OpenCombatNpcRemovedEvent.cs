namespace TakeInitiative.Api.Models;

public record OpenCombatNpcRemovedEvent
{
	public required Guid UserId {get; init;}
	public required Guid NpcId {get; init;}
}
