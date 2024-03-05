using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Controllers;

public record CombatResponse
{
	public required Combat Combat { get; set; }
}
