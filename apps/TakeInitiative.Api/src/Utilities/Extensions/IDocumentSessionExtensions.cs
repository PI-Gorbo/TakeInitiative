using Marten;
using TakeInitiative.Api.Features.Admin;

namespace TakeInitiative.Utilities.Extensions;
public static class IDocumentSessionExtensions
{
    public static async Task<bool> UserIsApartOfCampaign(this IDocumentSession session, Guid UserId, Guid CampaignId)
    {
        return await session.Query<CampaignMember>()
            .Where(x => x.UserId == UserId && x.CampaignId == CampaignId)
            .AnyAsync();
    }

    public static async Task<T?> LoadAdminConfig<T>(this IDocumentSession session, T? defaultIfNull = null) where T : class, IAdminConfig
    {
        return await session.Query<T>().FirstOrDefaultAsync() ?? defaultIfNull;
    }
}