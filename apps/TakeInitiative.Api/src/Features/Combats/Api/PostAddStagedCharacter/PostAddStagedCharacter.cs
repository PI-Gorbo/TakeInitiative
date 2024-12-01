using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public class PostAddStagedCharacter(IDocumentSession session, IHubContext<CombatHub> hubContext) : Endpoint<PostAddStagedCharacterRequest, CombatResponse>
{
    public override void Configure()
    {
        Post("/api/combat/stage/character");
    }

    public override async Task HandleAsync(PostAddStagedCharacterRequest req, CancellationToken ct)
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
                // Create the add user event
                StagedCharacterEvent addEvent = new()
                {
                    UserId = userId,
                    Character = new StagedCharacter(
                        Id: Guid.NewGuid(),
                        PlayerId: userId,
                        Name: req.Character.Name,
                        Initiative: req.Character.Initiative,
                        ArmourClass: req.Character.ArmourClass,
                        Health: req.Character.Health,
                        Hidden: req.Character.Hidden,
                        CharacterOriginDetails: CharacterOriginDetails.CustomCharacter(),
                        CopyNumber: null
                    )
                };
                session.Events.Append(req.CombatId, addEvent);
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