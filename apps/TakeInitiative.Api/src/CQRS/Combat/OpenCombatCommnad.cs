using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNet.SignalR;
using TakeInitiative.Api.Controllers;
using TakeInitiative.Api.Models;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.CQRS;

public record OpenCombatCommand : ICommand<Result<Combat>>
{
    public required Guid PlannedCombatId { get; set; }
    public required Guid UserId { get; set; }
}

public class OpenCombatCommandHandler(IDocumentStore Store) : CommandHandler<OpenCombatCommand, Result<Combat>>
{

    public override async Task<Result<Combat>> ExecuteAsync(OpenCombatCommand command, CancellationToken ct = default)
    {
        return await Store.Try(
			async (session) =>
			{
				// Retrieve the planned combat
				var plannedCombat = await session.LoadAsync<PlannedCombat>(command.PlannedCombatId, ct);
				if (plannedCombat == null)
				{
					ThrowError("There is no planned combat with the given id.", (int)HttpStatusCode.NotFound);
				}

				var campaign = await session.LoadAsync<Campaign>(plannedCombat.CampaignId, ct);
				if (campaign == null)
				{
					ThrowError("Cannot open a combat in a campaign that does not exist", (int)HttpStatusCode.NotFound);
				}

				// Check the user is a dm. 
				if (!campaign.CampaignMemberInfo.Single(x => x.UserId == command.UserId).IsDungeonMaster)
				{
					ThrowError("Only dungeon masters can open combats.", (int)HttpStatusCode.BadRequest);
				}

				// publish the event
				var openEvent = new CombatOpenedEvent()
				{
					UserId = command.UserId,
					CampaignId = plannedCombat.CampaignId,
					CombatName = plannedCombat.CombatName,
					Stages = plannedCombat.Stages,
				};

				var existingActiveCombat = campaign.ActiveCombatId != null;
				if (existingActiveCombat)
				{
					ThrowError("There is already an active combat.");
				}

				// Create a new stream
				var stream = session.Events.StartStream<Combat>(openEvent);

				// Delete the planned combat.
				session.Delete(plannedCombat);

				// Set the active combat id
				campaign.ActiveCombatId = stream.Id;
				session.Store(campaign);

				// Save changes, triggering the projection
				await session.SaveChangesAsync(ct);

				return await session.LoadAsync<Combat>(stream.Id, ct);
			});
    }
}

