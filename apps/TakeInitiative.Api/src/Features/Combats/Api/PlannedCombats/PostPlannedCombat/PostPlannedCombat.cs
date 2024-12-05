using FastEndpoints;

using Marten;

using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public class PostPlannedCombat(IDocumentStore Store) : Endpoint<PostPlannedCombatRequest, PlannedCombat>
{
    public override void Configure()
    {
        Post("/api/combat/planned");
    }

    public override async Task HandleAsync(PostPlannedCombatRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();

        var response = await new CreatePlannedCombatRequest()
        {
            UserId = userId, CampaignId = req.CampaignId, CombatName = req.CombatName,
        }.ExecuteAsync(ct);
        await this.ReturnApiResult(response);
    }
}