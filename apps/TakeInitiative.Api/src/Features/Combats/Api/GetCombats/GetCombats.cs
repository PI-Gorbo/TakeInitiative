using System.Net;
using FastEndpoints;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public class GetCombats(IDocumentStore Store) : EndpointWithoutRequest<GetCombatsResponse>
{
	public override void Configure()
	{
		Get("/api/combats/");
	}

	public override async Task HandleAsync(CancellationToken ct)
	{
		var campaignId = Query<Guid>("campaignId", isRequired: true);
		var userId = this.GetUserIdOrThrowUnauthorized();

		var result = await Store.Try(async session =>
		{
			var campaign = await session.LoadAsync<Campaign>(campaignId);
			if (campaign == null)
			{
				ThrowError((req) => campaignId, $"There is no campaign that corresponds to the id {campaignId}.");
			}

			return new GetCombatsResponse()
			{
				PlannedCombats = (campaign.IsDm(userId) switch
				{
					true => await session.Query<PlannedCombat>().Where(x => x.Id.IsOneOf(campaign.PlannedCombatIds))
						.Select(x => new PlannedCombatDto() { Id = x.Id, Name = x.CombatName }).ToListAsync(ct),
					false => [],
				}).ToArray(),
				Combats = (await session.Query<Combat>()
						.Where(x => x.CampaignId == campaignId && x.State == CombatState.Finished)
						.Select(x => new
						{
							x.Id, x.CombatName, x.State, x.FinishedTimestamp,
						})
						.ToListAsync(ct)
					)
					.Select(x => new CombatDto()
					{
						CombatId = x.Id,
						CombatName = x.CombatName!,
						State = x.State,
						FinishedTimestamp = x.FinishedTimestamp,
					}).ToArray()
			};
		});

		if (result.IsFailure)
		{
			ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
		}

		await SendAsync(result.Value);
	}
}