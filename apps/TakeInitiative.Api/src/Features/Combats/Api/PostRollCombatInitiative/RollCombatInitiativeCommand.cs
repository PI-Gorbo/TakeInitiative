using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;
namespace TakeInitiative.Api.Features.Combats;

public record RollCombatInitiativeCommand : ICommand<Result<Combat>>
{
    public required Guid CombatId { get; set; }
    public required Guid UserId { get; set; }
}

public class RollCombatInitiativeCommandHandler(IDocumentStore Store, IInitiativeRoller initiativeRoller, IHealthRoller healthRoller) : CommandHandler<RollCombatInitiativeCommand, Result<Combat>>
{

    public override async Task<Result<Combat>> ExecuteAsync(RollCombatInitiativeCommand command, CancellationToken ct = default)
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

                if (combat.State != CombatState.Started)
                {
                    ThrowError($"Combat has already been started.");
                }

                // Check the user is the dungeon master
                if (combat.DungeonMaster != command.UserId)
                {
                    ThrowError("Must be the dungeon master in order to start the combat.");
                }

                var computedInitiativeRolls = initiativeRoller.ComputeRolls(combat.StagedList);
                if (computedInitiativeRolls.IsFailure)
                {
                    ThrowError($"There was an error while trying to compute the dice rolls. {computedInitiativeRolls.Error}");
                }

                // Compute health rolls.
                var computedHealthRolls = healthRoller.ComputeRolls(combat.StagedList.ToList());
                if (computedHealthRolls.IsFailure)
                {
                    ThrowError($"There was an error while trying to compute health. {computedHealthRolls.Error}");
                }

                // Publish the event
                CombatInitiativeRolledEvent activateEvent = new()
                {
                    UserId = command.UserId,
                    EvaluatedRolls = computedHealthRolls.Value
                        .ToDictionary(x => x.Key, x => new EvaluatedCharacterRolls(
                            x.Value,
                            computedInitiativeRolls.Value.Single(init => init.Key == x.Key).Value
                        )),
                };
                session.Events.Append(command.CombatId, activateEvent);
                await session.SaveChangesAsync();

                combat = await session.LoadAsync<Combat>(command.CombatId);
                return combat;
            });
    }
}

