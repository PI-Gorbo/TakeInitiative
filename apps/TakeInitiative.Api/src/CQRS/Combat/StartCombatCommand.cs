using System.Collections.Immutable;
using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Marten.Exceptions;
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
                

                Result<ImmutableDictionary<Guid, int>> computedInitiativeRolls = combat.StagedList.Select(x => (id: x.Id, roll: x.Initiative.RollInitiative()))
                    .Aggregate(Result.Success(ImmutableDictionary<Guid, int>.Empty), (currentValue, nextValue) => {
                        if (currentValue.IsFailure) {
                            return currentValue.MapError(x => x + (nextValue.roll.IsFailure ? $", {nextValue.roll.Error}" : ""));
                        }


                        if (nextValue.roll.IsFailure) {
                            return nextValue.roll.ConvertFailure<ImmutableDictionary<Guid, int>>();
                        }

                        return currentValue.Value.Add(nextValue.id, nextValue.roll.Value);
                    });

                if (computedInitiativeRolls.IsFailure) {
                    ThrowError($"There was an issue while trying to compute the initiative rolls. {computedInitiativeRolls.Error}");
                }

                // Publish the event
                CombatStartedEvent activateEvent = new()
                {
                    UserId = command.UserId,
                    InitiativeRolls = computedInitiativeRolls.Value,
                };
                session.Events.Append(command.CombatId, activateEvent);
                await session.SaveChangesAsync();

                combat = await session.LoadAsync<Combat>(command.CombatId);
                return combat;
            });
    }
}

