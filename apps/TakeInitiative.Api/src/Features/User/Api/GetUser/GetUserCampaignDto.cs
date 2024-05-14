namespace TakeInitiative.Api.Features.Users;

public record GetUserCampaignDto(string CampaignName, Guid CampaignId, string JoinCode);
