using FastEndpoints;
using FluentValidation;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Campaigns;

public record PostCreateCampaignRequest
{
    public required string Name { get; init; }
}

public class PostCreateCampaignRequestValidator : Validator<PostCreateCampaignRequest>
{
    public PostCreateCampaignRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

/// <summary>Starts a Campaign stream and Session 1. The caller becomes its owner and first member, as a DM.</summary>
public class PostCreateCampaign(IDocumentSession session) : Endpoint<PostCreateCampaignRequest, CampaignResponse>
{
    private const int JoinCodeAttempts = 5;

    public override void Configure()
    {
        Post("/api/campaigns");
    }

    public override async Task HandleAsync(PostCreateCampaignRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var campaignId = Guid.NewGuid();
        var ownerMemberId = Guid.NewGuid();
        var joinCode = await UnusedJoinCode(ct);

        session.Events.StartStream<Campaign>(campaignId, new CampaignCreated(
            Actor: Actor.Member(ownerMemberId),
            Name: req.Name.Trim(),
            OwnerMemberId: ownerMemberId,
            OwnerUserId: userId,
            JoinCode: joinCode));
        // A new campaign starts with Session 1. Both streams are saved together, so they
        // share a transaction and a correlation id.
        session.Events.StartStream<Session>(Guid.NewGuid(),
            new SessionStarted(Actor.Member(ownerMemberId), campaignId, Number: 1));
        await session.SaveChangesAsync(ct);

        var campaign = await session.LoadAsync<Campaign>(campaignId, ct);
        await SendAsync(await CampaignResponse.Build(session, campaign!, userId, ct), cancellation: ct);
    }

    /// <summary>Codes are random; the unique index on JoinCode is the backstop for a race.</summary>
    private async Task<string> UnusedJoinCode(CancellationToken ct)
    {
        for (var attempt = 0; attempt < JoinCodeAttempts; attempt++)
        {
            var code = Campaign.NewJoinCode();
            if (!await session.Query<Campaign>().AnyAsync(c => c.JoinCode == code, ct))
            {
                return code;
            }
        }

        ThrowError("Could not generate a unique join code. Try again.", StatusCodes.Status503ServiceUnavailable);
        return default!;
    }
}
