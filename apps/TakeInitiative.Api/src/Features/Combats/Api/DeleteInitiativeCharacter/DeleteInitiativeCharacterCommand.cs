using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNet.SignalR;
using TakeInitiative.Api.Features;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.CQRS;

public record DeleteInitiativeCharacterCommand : ICommand<Result<Combat>>
{
    public required Guid CombatId { get; set; }
    public required Guid UserId { get; set; }
    public required Guid CharacterId { get; set; }
}

public class DeleteInitiativeCharacterCommandHandler(IDocumentStore store) : CommandHandler<DeleteInitiativeCharacterCommand, Result<Combat>>
{

    public override async Task<Result<Combat>> ExecuteAsync(DeleteInitiativeCharacterCommand command, CancellationToken ct = default)
    {
        return await store.Try(async (session) =>
        {
            var combat = await session.LoadAsync<Combat>(command.CombatId);
            if (combat == null)
            {
                ThrowError(x => x.CombatId, "Combat does not exist.");
            }

            // Ensure the combat is started.
            if (combat.State != CombatState.Started)
            {
                ThrowError(x => x.CombatId, "Combat's initiative list cannot be edited ");
            }

            // Ensure the player's character exists.
            var character = combat.InitiativeList.Find(x => x.Id == command.CharacterId).AsMaybe();
            if (character.HasNoValue)
            {
                ThrowError(x => x.CharacterId, "There is no character with the given id in the combat.");
            }

            // Ensure the player issuing the command is either the DM, or the player that own's the character.
            if (command.UserId != combat.DungeonMaster && command.UserId != character.Value.PlayerId)
            {
                ThrowError(x => x.UserId, "Only the dungeon master or the player who owns the character can edit it.");
            }

            // Publish the event
            InitiativeCharacterRemovedEvent @event = new()
            {
                UserId = command.UserId,
                CharacterId = command.CharacterId
            };
            session.Events.Append(command.CombatId, @event);
            await session.SaveChangesAsync();

            return await session.LoadAsync<Combat>(command.CombatId);
        });
    }
}

