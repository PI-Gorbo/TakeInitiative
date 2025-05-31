using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public record OpenCombatCommand : ICommand<Result<Combat>>
{
	public required Guid PlannedCombatId { get; init; }
	public required Guid UserId { get; init; }
}

public class OpenCombatCommandHandler(IDocumentStore Store) : CommandHandler<OpenCombatCommand, Result<Combat>>
{
	public override Task<Result<Combat>> ExecuteAsync(OpenCombatCommand command, CancellationToken ct = default)
	{
		return Store.Try(async (session) =>
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
			if (!campaign.IsDm(command.UserId))
			{
				ThrowError("Only dungeon masters can open combats.", (int)HttpStatusCode.BadRequest);
			}

			// publish the event
			var openEvent = new CombatStartedEvent()
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
			session.Store(campaign with
			{
				ActiveCombatId = stream.Id
			});

			// Save changes, triggering the projection
			await session.SaveChangesAsync(ct);

			return (await session.LoadAsync<Combat>(stream.Id, ct))!;
		});
	}
}