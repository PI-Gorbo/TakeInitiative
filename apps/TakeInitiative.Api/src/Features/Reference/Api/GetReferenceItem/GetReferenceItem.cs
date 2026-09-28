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
/// One reference item's stat block (20b.2), for any signed-in user. There is no campaign in the
/// route: reference content is the same for every campaign and every member, and nothing in it is
/// secret. An unknown provider or id is a 404, and so is a search-only provider's item, which has
/// no stat block (step 21: the app never shows 5eTools content). The data changes only with a
/// deploy, so the answer may be cached for a day.
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
        if (provider is null || provider.Get(req.ItemId) is not { StatBlock: { } statBlock } item)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        HttpContext.Response.Headers.CacheControl = CacheControl;
        await SendAsync(ReferenceItemResponse.From(provider, item, statBlock), cancellation: ct);
    }
}
