namespace TakeInitiative.Api.Models;
public record ActiveCharacterRemovedEvent { 
	public required Guid UserId {get; set;}
	public required Guid CharacterId {get; set;}
}

