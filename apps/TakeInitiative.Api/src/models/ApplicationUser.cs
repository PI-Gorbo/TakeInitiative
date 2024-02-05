using Microsoft.AspNetCore.Identity;

namespace TakeInitiative.Api.Models;
public class ApplicationUser : IdentityUser<Guid>
{
    public List<Guid> OwnedCampaignIds { get; set; } = new();
    public List<Guid> JoinedCampaignIds { get; set; } = new();
    public List<Guid> CampaignMemberInfoIds { get; set; } = new();
}