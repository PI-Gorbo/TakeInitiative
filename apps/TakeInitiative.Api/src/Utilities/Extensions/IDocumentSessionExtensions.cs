using Marten;
using TakeInitiative.Api.Bootstrap;

namespace TakeInitiative.Utilities.Extensions;
public static class IDocumentSessionExtensions
{
    /// <summary>
    /// Copies <paramref name="request"/>'s provenance onto a session opened alongside it, and
    /// returns that session so it can be opened and stamped in one expression.
    /// <para>
    /// <see cref="CorrelationMiddleware"/> stamps the correlation id and the <c>request</c> header
    /// on the request-scoped session only, so a session opened from the store carries neither and
    /// every event appended through it would lose invariant 9's provenance. Every write that needs
    /// a session of its own — one per retry attempt (30d), or one per transaction
    /// (<see cref="Features.Images.NoteWrite"/>) — passes it through here.
    /// </para>
    /// </summary>
    public static IDocumentSession WithProvenanceOf(this IDocumentSession session, IDocumentSession request)
    {
        session.CorrelationId = request.CorrelationId;
        session.CausationId = request.CausationId;
        if (request.GetHeader(CorrelationMiddleware.RequestHeaderKey) is { } header)
        {
            session.SetHeader(CorrelationMiddleware.RequestHeaderKey, header);
        }
        return session;
    }

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
}
