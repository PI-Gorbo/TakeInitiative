using TakeInitiative.Api.Models;

namespace TakeInitiative.Api.Controllers;

public record ActiveCombatResponse
{
	public required Combat Combat { get; set; }
}
