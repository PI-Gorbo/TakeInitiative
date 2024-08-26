using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public class PutUpsertStagedCharacter(IDocumentSession session, IHubContext<CombatHub> hubContext) : Endpoint<PutUpsertStagedCharacterRequest, CombatResponse>
{
    public override void Configure()
    {
        Put("/api/combat/stage/character");
    }

    public override async Task HandleAsync(PutUpsertStagedCharacterRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var result = await Result
            .Try(
                async () => await session.LoadAsync<Combat>(req.CombatId),
                err => ApiError.DbInteractionFailed(err.Message)
            )
                .EnsureNotNull("There is no combat with the given id.")
                .Ensure(
                    combat => combat.State != CombatState.Paused && combat.State != CombatState.Finished,
                    combat => $"Cannot stage character because the combat is {combat.State.ToString().ToLower()}.")
            .Bind(async fetchedCombat =>
            {
                Maybe<StagedCharacter> existingCharacter = fetchedCombat.StagedList.SingleOrDefault(x => x.Id == req.Character.Id).AsMaybe();
                if (!existingCharacter.HasValue)
                {
                    return ApiError.Invalid<PutUpsertStagedCharacterRequest>(x => x.Character.Id, "Character with the provided id does not exist.");
                }

                var userIsAllowedToEditCharacter = existingCharacter.Value.PlayerId == userId || fetchedCombat.DungeonMaster == userId;
                if (!userIsAllowedToEditCharacter)
                {
                    return ApiError.Invalid<PutUpsertStagedCharacterRequest>(x => x.Character, "Only a dungeon master can edit this character.");
                }

                StagedCharacter updatedCharacter = existingCharacter.Value with
                {
                    Name = req.Character.Name,
                    Health = req.Character.Health,
                    Initiative = req.Character.Initiative,
                    ArmourClass = req.Character.ArmourClass,
                    Hidden = req.Character.Hidden,
                };

                // Create the edit user event
                StagedCharacterEditedEvent editEvent = new()
                {
                    UserId = userId,
                    Character = updatedCharacter
                };
                session.Events.Append(req.CombatId, editEvent);
                await session.SaveChangesAsync();

                var refreshedCombat = await session.LoadAsync<Combat>(req.CombatId);
                await hubContext.NotifyCombatUpdated(refreshedCombat!);

                return Result.Success<CombatResponse, ApiError>(new CombatResponse()
                {
                    Combat = refreshedCombat!
                });
            });

        await this.ReturnApiResult(result);
    }
}