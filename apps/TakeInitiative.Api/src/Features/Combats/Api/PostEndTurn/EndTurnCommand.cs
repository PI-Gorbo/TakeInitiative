using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public record EndTurnCommand : ICommand<Result<Combat>>
{
    public required Guid CombatId { get; set; }
    public required Guid UserId { get; set; }
}

public class EndTurnCommandHandler(IDocumentStore Store) : CommandHandler<EndTurnCommand, Result<Combat>>
{

    public override async Task<Result<Combat>> ExecuteAsync(EndTurnCommand command, CancellationToken ct = default)
    {
        return await Store.Try(
            async (session) =>
            {
                var combat = await session.LoadAsync<Combat>(command.CombatId);
                if (combat == null)
                {
                    ThrowError(x => x.CombatId, "Combat does not exist.");
                }

                if (!combat.InitiativeIndex.HasValue || combat.State != CombatState.InitiativeRolled)
                {
                    ThrowError(x => x.CombatId, "Combat is not in the correct state.");
                }

                // Ensure that it is currently the user's turn, or the user is the dungeon master 
                var canFinishTurn = combat.DungeonMaster == command.UserId ||
                    combat.InitiativeList[combat.InitiativeIndex!.Value].PlayerId == command.UserId;
                if (!canFinishTurn)
                {
                    ThrowError("The turn can only be ended by either the dungeon master or the player currently taking their turn.");
                }

                // Publish the event
                TurnEndedEvent @event = new()
                {
                    UserId = command.UserId,
                };
                session.Events.Append(command.CombatId, @event);
                await session.SaveChangesAsync();

                return await session.LoadAsync<Combat>(command.CombatId);
            });
    }
}

