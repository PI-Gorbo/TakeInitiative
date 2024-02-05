namespace TakeInitiative.Api.Controllers;

public record LoginResponse
{
	public required string Token { get; set; }
}
