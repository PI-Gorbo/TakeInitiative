using System.Net;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Campaigns;

public class GetCampaign(IDocumentStore Store) : Endpoint<GetCampaignRequest, GetCampaignResponse>
{
    public override void Configure()
    {
        Get("/api/campaign/{CampaignId}");
    }

    public override async Task HandleAsync(GetCampaignRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();

        var result = await Store.Try(
            async (session) =>
            {
                var campaign = await session.LoadAsync<Campaign>(req.CampaignId, ct);
                if (campaign == null)
                {
                    ThrowError("There is no campaign with the given id.", (int)HttpStatusCode.NotFound);
                }

                // Check the user is one of the members of the campaign.
                var userIsAMember = campaign.CampaignMemberInfo.Any(x => x.UserId == userId);
                if (!userIsAMember)
                {
                    ThrowError("You cannot retrieve a campaign you are not apart of.", (int)HttpStatusCode.BadRequest);
                }

                // Retrieve all campaign members in the campaign
                var campaignMembers = await session.LoadManyAsync<CampaignMember>(campaign.CampaignMemberInfo.Select(x => x.MemberId));
                if (campaignMembers == null)
                {
                    ThrowError("Failed to retrieve campaign members for the campaign.", (int)HttpStatusCode.NotFound);
                }

                var userCampaignMember = campaignMembers?.SingleOrDefault(x => x.UserId == userId);
                if (userCampaignMember == null)
                {
                    ThrowError("Could not find user campaign member info.", (int)HttpStatusCode.NotFound);
                }

                // Retrieve all application user instances.
                var otherUserIds = campaignMembers!.Where(x => x.UserId != userId).Select(x => x.UserId).ToList();
                var userDtos = await session.Query<ApplicationUser>()
                    .Where(x => x.Id.IsOneOf(otherUserIds))
                    .Select(x => new { x.UserName, x.Id })
                    .ToListAsync();

                var nonUserCampaignMemberDtos = campaignMembers!.Where(x => x.UserId != userId)
                    .Select(member => CampaignMemberDto.FromMember(member, userDtos.Single(x => x.Id == member.UserId).UserName));

                // If the user is a dm, retrieve all the planned combats.
                var plannedCombats = userCampaignMember.IsDungeonMaster ? (await session.LoadManyAsync<PlannedCombat>(campaign.PlannedCombatIds)).ToArray() : null;

                // If there is a active combat, load it
                var dto = campaign.ActiveCombatId == null
                    ? null
                    : await session.Query<Combat>()
                        .Where(x => x.Id == campaign.ActiveCombatId)
                        .Select(x => new
                        {
                            x.Id,
                            x.State,
                            x.CombatName,
                            x.CurrentPlayers,
                            x.DungeonMaster,
                        })
                        .SingleOrDefaultAsync();

                int totalCombats = await session.Query<Combat>()
                    .Where(x => x.CampaignId == campaign.Id)
                    .CountAsync();
                DateTimeOffset? lastCombatTimestamp = totalCombats != 0
                    ? await session.Query<Combat>()
                        .Where(x => x.CampaignId == campaign.Id && x.FinishedTimestamp != null)
                        .OrderByDescending(x => x.FinishedTimestamp)
                        .Select(x => x.FinishedTimestamp)
                        .FirstOrDefaultAsync()
                    : null;

                return new GetCampaignResponse()
                {
                    Campaign = campaign,
                    JoinCode = CampaignIdShortener.ToShortId(campaign.Id),
                    NonUserCampaignMembers = nonUserCampaignMemberDtos.ToArray(),
                    UserCampaignMember = userCampaignMember,
                    CurrentCombatInfo = dto == null ? null : new CurrentCombatDto()
                    {
                        CombatName = dto.CombatName,
                        CurrentPlayers = dto.CurrentPlayers.ToList(),
                        DungeonMaster = dto.DungeonMaster,
                        Id = dto.Id,
                        State = dto.State
                    },
                    CombatHistoryInfo = new CombatHistoryDto()
                    {
                        TotalCombats = totalCombats,
                        LastCombatTimestamp = lastCombatTimestamp
                    }
                };
            });

        if (result.IsFailure)
        {
            ThrowError(result.Error, (int)HttpStatusCode.ServiceUnavailable);
        }

        await SendAsync(result.Value);
    }
}