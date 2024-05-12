using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNet.SignalR;
using TakeInitiative.Api.Features;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.CQRS;

public record FinishCombatCommand : ICommand<Result<Combat>>
{
    public required Guid CombatId { get; set; }
    public required Guid UserId { get; set; }
}

public class FinishCombatCommandHandler(IDocumentStore Store) : CommandHandler<FinishCombatCommand, Result<Combat>>
{

    public override async Task<Result<Combat>> ExecuteAsync(FinishCombatCommand command, CancellationToken ct = default)
    {
        return await Store.Try(
            async (session) =>
            {
                var combat = await session.LoadAsync<Combat>(command.CombatId);
                if (combat == null)
                {
                    ThrowError(x => x.CombatId, "Combat does not exist.");
                }

                // Check the user is the dungeon master
                if (combat.DungeonMaster != command.UserId)
                {
                    ThrowError("Must be the dungeon master in order to finish the combat.");
                }

                // Remove the active combat from the campaign
                var campaign = await session.LoadAsync<Campaign>(combat.CampaignId);
                if (campaign == null)
                {
                    ThrowError("An error occurred while trying to fetch the campaign for the combat.");
                }
                campaign.ActiveCombatId = null;
                session.Store(campaign);

                // Publish the event
                CombatFinishedEvent @event = new()
                {
                    UserId = command.UserId,
                };
                session.Events.Append(command.CombatId, @event);
                await session.SaveChangesAsync();

                return await session.LoadAsync<Combat>(command.CombatId);
            });
    }
}

