using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNet.SignalR;
using TakeInitiative.Api.Controllers;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.CQRS;

public record EditInitiativeCharacterCommand : ICommand<Result<Combat>>
{
    public required Guid CombatId { get; set; }
    public required Guid UserId { get; set; }
}

public class EditInitiativeCharacterCommandHandler(IDocumentSession session) : CommandHandler<EditInitiativeCharacterCommand, Result<Combat>>
{

    public override async Task<Result<Combat>> ExecuteAsync(EditInitiativeCharacterCommand command, CancellationToken ct = default)
    {
        return await Result.Try(async () => {
            var combat = await session.LoadAsync<Combat>(command.CombatId);
            if (combat == null)
            {
                ThrowError(x => x.CombatId, "Combat does not exist.");
            }

            // Ensure that it is currently the user's turn, or the user is the dungeon master 
            var canFinishTurn = combat.DungeonMaster == command.UserId ||
                combat.InitiativeList[combat.InitiativeIndex].PlayerId == command.UserId;
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

