using System.Collections.Immutable;
using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Marten.Exceptions;
using Microsoft.AspNet.SignalR;
using TakeInitiative.Api.Features;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.CQRS;

public record RollStagedCharacterIntoInitiativeCommand : ICommand<Result<Combat>>
{
    public required Guid CombatId { get; set; }
    public required Guid UserId { get; set; }
    public required Guid[] CharacterIds { get; set; }
}

public class RollStagedCharacterIntoInitiativeCommandHandler(IDocumentStore Store) : CommandHandler<RollStagedCharacterIntoInitiativeCommand, Result<Combat>>
{

    public override async Task<Result<Combat>> ExecuteAsync(RollStagedCharacterIntoInitiativeCommand command, CancellationToken ct = default)
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

                // Check the user is the dungeon master.
                if (combat.DungeonMaster != command.UserId)
                {
                    ThrowError("Must be the dungeon master in order to start the combat.");
                }

                // Check that all the characters specified exist.
                var stagedCharacterIds = combat.StagedList.Select(x => x.Id).ToList();
                if (command.CharacterIds.Any(character => !stagedCharacterIds.Contains(character)))
                {
                    ThrowError($"One or more of the specified character ids are not in the staged character list.");
                }

                var computedInitiativeRolls = DiceRoller.ComputeRollsWithExistingInitiative(combat.StagedList.Where(x => x.Id.In(command.CharacterIds)), combat.InitiativeList);
                if (computedInitiativeRolls.IsFailure)
                {
                    ThrowError($"There was an error while trying to compute the dice rolls. {computedInitiativeRolls.Error}");
                }

                // Publish the event
                StagedCharactersRolledIntoInitiativeEvent activateEvent = new()
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

