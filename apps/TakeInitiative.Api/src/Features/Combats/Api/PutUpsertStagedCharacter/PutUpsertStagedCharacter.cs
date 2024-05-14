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
        AuthSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
        Policies(TakePolicies.UserExists);
    }

    public override async Task HandleAsync(PutUpsertStagedCharacterRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        await Result
            .Try(
                async () => await session.LoadAsync<Combat>(req.CombatId),
                err => ApiError.DbInteractionFailed(err.Message))
            .EnsureNotNull("There is no combat with the given id.")
            .Ensure(
                combat => combat.State != CombatState.Paused && combat.State != CombatState.Finished,
                combat => $"Cannot stage character because the combat is {combat.State.ToString().ToLower()}.")
            .Ensure(combat => combat.CurrentPlayers.Any(x => x.UserId == userId), "Must be a current player in order to stage enemies")
            .Bind(async fetchedCombat =>
            {
                var existingCharacter = fetchedCombat.StagedList.SingleOrDefault(x => x.Id == req.Character.Id);
                var character = new CombatCharacter()
                {
                    Id = req.Character.Id,
                    PlayerId = existingCharacter?.PlayerId ?? userId,
                    Name = req.Character.Name,
                    Initiative = req.Character.Initiative,
                    Health = req.Character.Health,
                    ArmorClass = req.Character.ArmorClass,
                    Hidden = req.Character.Hidden,
                    InitiativeValue = null,
                    PlannedCharacterId = null,
                    CopyNumber = null,
                };

                if (existingCharacter != null)
                {
                    var userIsAllowedToEditCharacter = existingCharacter?.PlayerId == userId || fetchedCombat.DungeonMaster == userId;
                    if (!userIsAllowedToEditCharacter)
                    {
                        return ApiError.Invalid<PutUpsertStagedCharacterRequest>(x => x.Character, "Only a dungeon master can edit this character.");
                    }

                    // Create the edit user event
                    StagedCharacterEditedEvent editEvent = new()
                    {
                        UserId = userId,
                        Character = character
                    };
                    session.Events.Append(req.CombatId, editEvent);
                }
                else
                {
                    // Create the add user event
                    StagedCharacterEvent addEvent = new()
                    {
                        UserId = userId,
                        Character = character
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
            }).ToApiResult(this);
    }
}