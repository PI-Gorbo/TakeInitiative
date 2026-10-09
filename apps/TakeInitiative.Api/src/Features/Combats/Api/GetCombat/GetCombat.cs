using FastEndpoints;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public record GetCombatRequest
{
    public Guid CampaignId { get; init; }
    public Guid CombatId { get; init; }
}

/// <summary>
/// One combat as the caller may see it (<see cref="CombatView"/>). A Draft, for a player, and
/// another campaign's combat are 404s.
/// </summary>
public class GetCombat(IDocumentSession session) : Endpoint<GetCombatRequest, CombatResponse>
{
    public override void Configure()
    {
        Get("/api/campaigns/{CampaignId}/combats/{CombatId}");
    }

    public override async Task HandleAsync(GetCombatRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var combat = await this.RequireVisibleCombat(session, req.CampaignId, req.CombatId, member, ct);
        await SendAsync(await CombatEntries.ViewFor(session, combat, member, ct), cancellation: ct);
    }
}
