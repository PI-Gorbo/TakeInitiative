using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public record EditInitiativeCharacterCommand : ICommand<Result<Combat>>
{
    public required Guid CombatId { get; set; }
    public required Guid UserId { get; set; }
    public required CombatCharacterDto CharacterDto { get; set; }
}

public class EditInitiativeCharacterCommandHandler(IDocumentSession session) : CommandHandler<EditInitiativeCharacterCommand, Result<Combat>>
{

    public override Task<Result<Combat>> ExecuteAsync(EditInitiativeCharacterCommand command, CancellationToken ct = default)
    {
        return Result.Try(() => session.LoadAsync<Combat>(command.CombatId), ex => "Failed initial fetch of the combat from the database.")
            .EnsureNotNull("Combat does not exist.")
            .Ensure(combat => combat.State == CombatState.InitiativeRolled, "Combat's initiative list cannot be edited ")
            .Bind((combat) =>
            {
                // Ensure the player's character exists.
                var character = combat.InitiativeList.Find(x => x.Id == command.CharacterDto.Id).AsMaybe();
                if (character.HasNoValue)
                {
                    return Result.Failure<InitiativeCharacterEditedEvent>("There is no character with the given id in the combat.");
                }

                // Ensure the player issuing the command is either the DM, or the player that own's the character.
                if (command.UserId != combat.DungeonMaster && command.UserId != character.Value.PlayerId)
                {
                    return Result.Failure<InitiativeCharacterEditedEvent>("Only the dungeon master or the player who owns the character can edit it.");
                }

                // Check if the initiative value has changed. If it has, validate the change.
                if (!character.Value.InitiativeValue.SequenceEqual(command.CharacterDto.InitiativeValue))
                {
                    throw new NotImplementedException("Not implemented yet.");
                }

                // Publish the event
                return new InitiativeCharacterEditedEvent()
                {
                    UserId = command.UserId,
                    Character = command.CharacterDto
                };
            }).TapTry(async (@event) =>
            {
                session.Events.Append(command.CombatId, @event);
                await session.SaveChangesAsync();
            }, ex => $"Failed to save event to the database. Error: {ex.Message}. Full error: {ex}.")
            .MapTry(async (@event) =>
            {
                var combat = await session.LoadAsync<Combat>(command.CombatId);
                return combat!;
            }, ex => $"Failed to load combat from the database. Error: {ex.Message}. Full error: {ex}.");
    }
}

