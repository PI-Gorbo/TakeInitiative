using CSharpFunctionalExtensions;
using FastEndpoints;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;


public class PostStagePlayerCharacters(IHubContext<CombatHub> hubContext) : Endpoint<PostStagePlayerCharactersRequest, CombatResponse>
{
    public override void Configure()
    {
        Post("/api/combat/stage/player-character");
    }

    public override async Task HandleAsync(PostStagePlayerCharactersRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();

        Result<Combat, ApiError> result = await new StagePlayerCharactersCommand()
        {
            CombatId = req.CombatId,
            UserId = userId,
            CharacterIds = req.CharacterIds,
        }.ExecuteAsync();

        await this.ReturnApiResult(result.Map(val => new CombatResponse() { Combat = val }));
        await hubContext.NotifyCombatUpdated(result.Value);
    }
}