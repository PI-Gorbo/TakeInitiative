using Microsoft.AspNetCore.Identity;

namespace TakeInitiative.Api.Models;
public class ApplicationUser : IdentityUser<Guid>
{
	public List<Guid> Campaigns {get; set;} = new();
}