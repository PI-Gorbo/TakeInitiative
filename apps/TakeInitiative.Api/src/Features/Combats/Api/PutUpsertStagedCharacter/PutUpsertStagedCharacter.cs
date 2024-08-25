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
                Maybe<CombatCharacter> existingCharacter = fetchedCombat.StagedList.SingleOrDefault(x => x.Id == req.Character.Id).AsMaybe();

                if (existingCharacter.HasValue)
                {
                    var userIsAllowedToEditCharacter = existingCharacter.Value.PlayerId == userId || fetchedCombat.DungeonMaster == userId;
                    if (!userIsAllowedToEditCharacter)
                    {
                        return ApiError.Invalid<PutUpsertStagedCharacterRequest>(x => x.Character, "Only a dungeon master can edit this character.");
                    }

                    CombatCharacter updatedCharacter = existingCharacter.Value with
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
                }
                else
                {
                    // Create the add user event
                    StagedCharacterEvent addEvent = new()
                    {
                        UserId = userId,
                        Character = CombatCharacter.NewCombatCharacter(
                            Id: Guid.NewGuid(),
                            playerId: userId,
                            name: req.Character.Name,
                            initiative: req.Character.Initiative,
                            armourClass: req.Character.ArmourClass,
                            health: req.Character.Health,
                            hidden: req.Character.Hidden,
                            characterOriginDetails: CharacterOriginDetails.CustomCharacter(),
                            copyNumber: null
                        )
                    };
                    session.Events.Append(req.CombatId, addEvent);
                }

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