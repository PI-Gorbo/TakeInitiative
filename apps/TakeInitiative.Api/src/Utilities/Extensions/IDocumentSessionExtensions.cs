using Marten;
using TakeInitiative.Api.Features.Admin;

namespace TakeInitiative.Utilities.Extensions;
public static class IDocumentSessionExtensions
{
    /// <summary>Membership is read from the Campaign projection, the only place it is stored.</summary>
    public static async Task<bool> UserIsApartOfCampaign(this IQuerySession session, Guid UserId, Guid CampaignId)
    {
        return await session.Query<Campaign>()
            .Where(c => c.Id == CampaignId && c.Members.Any(m => m.UserId == UserId))
            .AnyAsync();
    }

    /// <summary>Every campaign the user is a member of.</summary>
    public static async Task<IReadOnlyList<Campaign>> CampaignsForUser(this IQuerySession session, Guid userId, CancellationToken ct = default)
    {
        return await session.Query<Campaign>()
            .Where(c => c.Members.Any(m => m.UserId == userId))
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }

    public static async Task<T?> LoadAdminConfig<T>(this IDocumentSession session, T? defaultIfNull = null) where T : class, IAdminConfig
    {
        return await session.Query<T>().FirstOrDefaultAsync() ?? defaultIfNull;
    }
}
