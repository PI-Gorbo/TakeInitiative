using FastEndpoints;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Campaigns;

public record GetCampaignsResponse
{
    public required CampaignSummary[] Campaigns { get; init; }
}

public record CampaignSummary
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    /// <summary>The caller's role in the campaign.</summary>
    public required Role Role { get; init; }
    /// <summary>Whether the caller owns the campaign.</summary>
    public required bool IsOwner { get; init; }
    public required int MemberCount { get; init; }
}

/// <summary>The caller's campaigns, read from the Campaign projection.</summary>
public class GetCampaigns(IDocumentSession session) : EndpointWithoutRequest<GetCampaignsResponse>
{
    public override void Configure()
    {
        Get("/api/campaigns");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var campaigns = await session.CampaignsForUser(userId, ct);

        await SendAsync(new GetCampaignsResponse
        {
            Campaigns = campaigns
                .Select(c =>
                {
                    var me = c.MemberForUser(userId)!;
                    return new CampaignSummary
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Role = me.Role,
                        IsOwner = c.IsOwner(me.MemberId),
                        MemberCount = c.Members.Count,
                    };
                })
                .ToArray(),
        }, cancellation: ct);
    }
}
