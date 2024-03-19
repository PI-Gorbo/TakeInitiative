using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNet.SignalR;
using TakeInitiative.Api.Controllers;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.CQRS;

public record StartCombatCommand : ICommand<Result<Combat>>
{
    public required Guid CombatId { get; set; }
    public required Guid UserId { get; set; }
}

public class StartCombatCommandHandler(IDocumentStore Store) : CommandHandler<StartCombatCommand, Result<Combat>>
{

    public override async Task<Result<Combat>> ExecuteAsync(StartCombatCommand command, CancellationToken ct = default)
    {
        return await Store.Try(
            async (session) =>
            {
                var combat = await session.LoadAsync<Combat>(command.CombatId);
                if (combat == null)
                {
                    ThrowError(x => x.CombatId, "Combat does not exist.");
                }

                // Check the state of the combat.
                if (combat.State == CombatState.Paused || combat.State == CombatState.Finished)
                {
                    ThrowError($"Cannot activate character because the combat is {combat.State.ToString().ToLower()}.");
                }

                if (combat.State != CombatState.Open)
                {
                    ThrowError($"Combat has already been started.");
                }

                // Check the user is part of the combat.
                if (combat.DungeonMaster != command.UserId)
                {
                    ThrowError("Must be the dungeon master in order to start the combat.");
                }

                // Publish the event
                CombatStartedEvent activateEvent = new()
                {
                    UserId = command.UserId,
                };
                session.Events.Append(command.CombatId, activateEvent);
                await session.SaveChangesAsync();

                combat = await session.LoadAsync<Combat>(command.CombatId);
                return combat;
            });
    }
}

