using Marten.Linq;
using Marten;

namespace TakeInitiative.Utilities.Extensions;
public static class IMartenQueryableExtensions
{
    public static Task<CampaignMember?> GetCampaignMemberForUserAndCampaign(this IMartenQueryable<CampaignMember> query, Guid userId, Guid campaignId)
    {
        return query.SingleOrDefaultAsync((x) => x.UserId == userId && x.CampaignId == campaignId);
    }
}