using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNet.SignalR;
using TakeInitiative.Api.Controllers;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.CQRS;

public record StagePlannedCharactersCommand : ICommand<Result<Combat>>
{
    public required Guid UserId { get; set; }
    public required Guid CombatId {get; set;}
    public required Dictionary<Guid, StagePlannedCharacterDto[]> PlannedCharactersToStage {get; set;}
}

public class StagePlannedCharactersCommandHandler(IDocumentStore Store) : CommandHandler<StagePlannedCharactersCommand, Result<Combat>>
{

    public override async Task<Result<Combat>> ExecuteAsync(StagePlannedCharactersCommand command, CancellationToken ct = default)
    {
        return await Store.Try(
			async (session) =>
			{
                var combat = await session.LoadAsync<Combat>(command.CombatId);
				if (combat == null)
				{
					ThrowError(x => x.CombatId, "Combat does not exist.");
				}					

				// Check the user is part of the combat.
				if (combat.DungeonMaster != command.UserId)
				{
					ThrowError("Must be the dungeon master in order to finish the combat.");
                }

                // Validate the request.
                var nonExistentPlannedCombatIds = command.PlannedCharactersToStage.Keys.Except(combat.PlannedStages.Select(x => x.Id));
                if (nonExistentPlannedCombatIds.Any()) {
                    ThrowError(x => x.PlannedCharactersToStage, $"One or more of the provided planned combat ids do not exist. {string.Join(", ", nonExistentPlannedCombatIds)}");
                }

                // Check, for each character referenced, that the character exists and the quantity is valid.
                foreach (var keyValue in command.PlannedCharactersToStage) {
                    if (keyValue.Value.Length == 0) {
                        continue;
                    }
                    var existingStage = combat.PlannedStages.Single(x => x.Id == keyValue.Key);

                    foreach (var character in keyValue.Value) {

                        // Check that the character exists and the quantity is valid.
                        var existingChar = existingStage.Npcs.SingleOrDefault(x => x.Id == character.CharacterId);
                        if (existingChar == null) {
                            ThrowError(x => x.PlannedCharactersToStage, $"The character with id {character.CharacterId} does not exist in the stage {existingStage.Name}.");
                        }

                        if (character.Quantity < 1) {
                            ThrowError(x => x.PlannedCharactersToStage, $"Quantity must be at least 1.");
                        }

                        if (character.Quantity > existingChar.Quantity) {
                            ThrowError(x => x.PlannedCharactersToStage, $"Cannot specify a higher quantity than remaining planned characters.");
                        }
                    }
                }

				// Publish the event
				StagedPlannedCharacterEvent @event = new()
				{
					CombatId = command.CombatId,
                    PlannedCharactersToStage = command.PlannedCharactersToStage,
                    UserId = command.UserId
				};
				session.Events.Append(command.CombatId, @event);
				await session.SaveChangesAsync();

				return await session.LoadAsync<Combat>(command.CombatId);
			});
    }
}

