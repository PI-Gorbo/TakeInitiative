using Marten.Linq;
using Marten;

namespace TakeInitiative.Utilities.Extensions;
public static class IMartenQueryableExtensions
{
    public static Task<CampaignMember?> GetCampaignMemberForUserAndCampaign(this IMartenQueryable<CampaignMember> query, Guid userId, Guid campaignId)
    {
        return query.SingleOrDefaultAsync((x) => x.UserId == userId && x.CampaignId == campaignId);
    }

    public static async Task<bool> CombatNameIsUnique(this IMartenQueryable<PlannedCombat> query, Guid campaignId, string combatName)
    {
        return await query.Where(x => x.CampaignId == campaignId && x.CombatName == combatName)
                    .CountAsync() == 0;
    }
}