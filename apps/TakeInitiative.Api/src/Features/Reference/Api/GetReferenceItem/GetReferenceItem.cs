using FastEndpoints;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Reference;

public record GetReferenceItemRequest
{
    /// <summary>The provider's key: <c>srd52</c>.</summary>
    public string Provider { get; init; } = "";
    /// <summary>The item's id within its provider: <c>goblin-warrior</c>.</summary>
    public string ItemId { get; init; } = "";
}

/// <summary>
/// One reference item (20b.2), for any signed-in user. There is no campaign in the route:
/// reference content is the same for every campaign and every member, and nothing in it is secret.
/// A provider with stat blocks answers the stat block. A search-only provider (5eTools, 21b.5)
/// answers the summary with <c>statBlock: null</c>: the summary holds only index fields, which the
/// ⌘K row already shows, and gives + Wiki and "Use … stats" the item's Stats; no 5eTools content
/// leaves the server. An unknown provider or id is a 404. The data changes only with a deploy (or
/// a restart with a new index), so the answer may be cached for a day.
/// </summary>
public class GetReferenceItem(ReferenceCatalog catalog) : Endpoint<GetReferenceItemRequest, ReferenceItemResponse>
{
    public const string CacheControl = "private, max-age=86400";

    public override void Configure()
    {
        Get("/api/reference/{Provider}/{ItemId}");
        Description(b => b.ProducesProblemFE(StatusCodes.Status404NotFound));
    }

    public override async Task HandleAsync(GetReferenceItemRequest req, CancellationToken ct)
    {
        this.GetUserIdOrThrowUnauthorized();

        var provider = catalog.Get(req.Provider);
        var item = provider switch
        {
            null => null,
            { HasStatBlocks: true } => await provider.Get(req.ItemId, ct) is { StatBlock: not null } full ? full : null,
            _ => await provider.Find(req.ItemId, ct) is { } summary ? new ReferenceItem(summary, null, provider.Attribution) : null,
        };
        if (item is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        HttpContext.Response.Headers.CacheControl = CacheControl;
        await SendAsync(ReferenceItemResponse.From(provider!, item), cancellation: ct);
    }
}
