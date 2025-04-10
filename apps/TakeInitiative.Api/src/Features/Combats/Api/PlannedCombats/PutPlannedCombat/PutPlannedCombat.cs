using FastEndpoints;

using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public class PutPlannedCombat() : Endpoint<PutPlannedCombatRequest, PlannedCombat>
{
    public override void Configure()
    {
        Post("/api/combat/planned/{PlannedCombatId}");
    }

    public override async Task HandleAsync(PutPlannedCombatRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();

        var response = await new UpdatePlannedCombatRequest()
        {
            UserId = userId, PlannedCombatId = req.PlannedCombatId, CombatName = req.CombatName,
        }.ExecuteAsync(ct);
        await this.ReturnApiResult(response);
    }
}