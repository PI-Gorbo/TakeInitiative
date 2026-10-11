using FastEndpoints;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Campaigns;

public record PutCampaignNameRequest
{
    public Guid CampaignId { get; init; }
    public required string Name { get; init; }
}

public class PutCampaignNameRequestValidator : Validator<PutCampaignNameRequest>
{
    public PutCampaignNameRequestValidator()
    {
        RuleFor(x => Campaign.NormaliseName(x.Name))
            .NotEmpty()
            .MaximumLength(Campaign.NameMaxLength)
            .OverridePropertyName(nameof(PutCampaignNameRequest.Name));
    }
}

/// <summary>
/// A DM renames the campaign (glossary: settings management is a DM's). The same name appends
/// nothing. Every member sees the name, so the push goes to the whole campaign group.
/// </summary>
public class PutCampaignName(IDocumentSession session, IHubContext<CampaignHub> hub)
    : Endpoint<PutCampaignNameRequest, CampaignResponse>
{
    public override void Configure()
    {
        Put("/api/campaigns/{CampaignId}/name");
    }

    public override async Task HandleAsync(PutCampaignNameRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (campaign, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        this.RequireDm(member, "Only a DM can rename the campaign.");

        var name = Campaign.NormaliseName(req.Name);
        if (campaign.Name != name)
        {
            session.Events.Append(campaign.Id, new CampaignRenamed(Actor.Member(member.MemberId), name));
            await session.SaveChangesAsync(ct);
            campaign = (await session.LoadAsync<Campaign>(campaign.Id, ct))!;
            await hub.NotifyCampaignRenamed(campaign.Id, campaign.Name);
        }

        await SendAsync(await CampaignResponse.Build(session, campaign, userId, ct), cancellation: ct);
    }
}
