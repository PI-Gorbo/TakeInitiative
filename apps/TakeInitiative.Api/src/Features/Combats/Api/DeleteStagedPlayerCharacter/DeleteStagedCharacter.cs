using System.Net;
using FastEndpoints;
using Marten;
using TakeInitiative.Utilities.Extensions;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.SignalR;

namespace TakeInitiative.Api.Features.Combats;

public class DeleteStagedCharacter(IDocumentStore Store, IHubContext<CombatHub> hubContext) : Endpoint<DeleteStagedCharacterRequest, CombatResponse>
{
    public override void Configure()
    {
        Delete("/api/combat/stage/character");
    }

    public override async Task HandleAsync(DeleteStagedCharacterRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();

        Result<CombatResponse> result = await Store.Try(async (session) =>
        {
            var combat = await session.LoadAsync<Combat>(req.CombatId);
            if (combat == null)
            {
                ThrowError(x => x.CombatId, "Combat does not exist.");
            }

            // Check the state of the combat.
            if (combat.State == CombatState.Paused || combat.State == CombatState.Finished)
            {
                ThrowError($"Cannot stage character because the combat is {combat.State.ToString().ToLower()}.");
            }

            var character = combat.StagedList.SingleOrDefault(x => x.Id == req.CharacterId);
            if (character == null)
            {
                ThrowError(x => x.CharacterId, "There is no character with the given id.");
            }

            // Check the player is authorized to delete the staged character.
            bool isAuthorized = combat.DungeonMaster == userId || character.PlayerId == userId;
            if (!isAuthorized)
            {
                ThrowError("Only the dungeon master or the player that made this character can delete it from the staged list.");
            }

            // Publish the event
            StagedCharacterRemovedEvent removedEvent = new()
            {
                UserId = userId,
                CharacterId = req.CharacterId
            };
            session.Events.Append(req.CombatId, removedEvent);
            await session.SaveChangesAsync();

            combat = await session.LoadAsync<Combat>(req.CombatId);
            await hubContext.NotifyCombatUpdated(combat!);

            return new CombatResponse()
            {
                Combat = combat!
            };
        });

        if (result.IsFailure)
        {
            ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
        }

        await SendAsync(result.Value);
    }
}