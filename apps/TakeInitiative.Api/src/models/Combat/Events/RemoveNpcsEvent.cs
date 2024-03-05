namespace TakeInitiative.Api.Models;

public record RemoveNpcsEvent
{
	public required Guid UserId { get; init; }
	public required List<Guid> NpcIds { get; init; }
};
