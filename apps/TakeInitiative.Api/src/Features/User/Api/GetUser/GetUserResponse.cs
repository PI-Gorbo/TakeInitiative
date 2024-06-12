using CSharpFunctionalExtensions;
using Marten;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Users;

public class GetUserResponse
{
    public required Guid UserId { get; set; }
    public required string Username { get; set; }
    public required bool ConfirmedEmail { get; set; }
    public required List<GetUserCampaignDto> DmCampaigns { get; set; }
    public required List<GetUserCampaignDto> MemberCampaigns { get; set; }

    public static async Task<Result<GetUserResponse, ApiError>> Generate(IDocumentSession session, Guid userId)
    {
        return await Result.Try(async () =>
        {
            var user = await session.LoadAsync<ApplicationUser>(userId);
            var campaigns = await session.LoadManyAsync<Campaign>(user!.Campaigns);
            return new
            {
                user = user,
                campaigns = campaigns
            };
        }, err => ApiError.DbInteractionFailed(err.Message))
        .Ensure((data) => data.user is not null, ApiError.NotFound("There is no user with the given user id."))
        .Ensure(data => data.campaigns is not null, ApiError.NotFound("The user does not belong to any campaigns."))
        .Map(userData =>
        {
            var dmCampaigns = new List<GetUserCampaignDto>();
            var memberCampaigns = new List<GetUserCampaignDto>();
            foreach (Campaign campaign in userData.campaigns)
            {
                var member = campaign.CampaignMemberInfo.SingleOrDefault(x => x.UserId == userId);
                if (member!.IsDungeonMaster)
                {
                    dmCampaigns.Add(new GetUserCampaignDto(campaign.CampaignName, campaign.Id, CampaignIdShortener.ToShortId(campaign.Id)));
                }
                else
                {
                    memberCampaigns.Add(new GetUserCampaignDto(campaign.CampaignName, campaign.Id, CampaignIdShortener.ToShortId(campaign.Id)));
                }
            }

            return new GetUserResponse()
            {
                UserId = userData.user.Id,
                Username = userData.user.UserName!,
                ConfirmedEmail = userData.user.EmailConfirmed,
                DmCampaigns = dmCampaigns,
                MemberCampaigns = memberCampaigns
            };
        });
    }
}
