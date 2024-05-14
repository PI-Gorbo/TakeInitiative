using Marten;

namespace TakeInitiative.Utilities.Extensions;
public static class UserIDocumentSessionExtensions
{
    public static async Task<bool> UserIsApartOfCampaign(this IDocumentSession session, Guid UserId, Guid CampaignId)
    {
        return await session.Query<CampaignMember>()
            .Where(x => x.UserId == UserId && x.CampaignId == CampaignId)
            .AnyAsync();
    }
}